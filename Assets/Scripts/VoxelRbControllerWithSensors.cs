using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class VoxelRbControllerWithSensors : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot; // направление WASD берём по yaw камеры

    [Tooltip("Сенсор спереди (локально +Z), на +1 юнит выше, isTrigger CapsuleCollider r=0.5,h=2,center=(0,0.5,0)")]
    public StepSensor sensorForward;
    [Tooltip("Сенсор сзади (локально -Z)")]
    public StepSensor sensorBack;
    [Tooltip("Сенсор слева (локально -X)")]
    public StepSensor sensorLeft;
    [Tooltip("Сенсор справа (локально +X)")]
    public StepSensor sensorRight;

    [Tooltip("Маска твёрдых блоков/земли (игрок не должен входить).")]
    public LayerMask solidMask = ~0;

    [Header("Capsule (для кастов вперёд)")]
    public float capsuleRadius = 0.5f;  // = CapsuleCollider.radius
    public float capsuleHeight = 2.0f;  // = CapsuleCollider.height
    public Vector3 capsuleCenter = new Vector3(0, 0.5f, 0); // как на коллайдере

    [Header("Move")]
    public float moveSpeed = 6f;
    public float acceleration = 30f;      // разгон по плоскости
    public float airControl = 0.5f;       // управление в воздухе (0..1)
    public float rotateToCameraYaw = 720f;// скорость поворота yaw к камере (0 — не крутить)

    [Header("Step Up")]
    [Tooltip("Высота подъёма (воксель).")]
    public float stepHeight = 1f;         // по условию
    [Tooltip("Насколько выносим вперёд при подъёме, чтобы не сорваться назад.")]
    public float stepForwardNudge = 0.15f;
    [Tooltip("Длительность плавного подъёма (сек). 0 — телепорт.")]
    public float stepLiftDuration = 0.08f;

    [Header("Ground")]
    public float groundCheckDistance = 0.12f; // 0.1–0.15 под твои размеры ок
    public float coyoteTime = 0.1f;
    public float jumpSpeed = 6.5f;

    [Header("Debug")]
    public bool drawDebug;

    Rigidbody _rb;
    CapsuleCollider _cap;
    bool _controlEnabled = true;
    bool _stepping;
    bool _grounded;
    float _lastGroundTime;
    Vector2 _rawInput; // WASD

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _cap = GetComponent<CapsuleCollider>();

        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // подстрахуем значения по умолчанию под твой CapsuleCollider
        capsuleRadius = _cap.radius;
        capsuleHeight = _cap.height;
        capsuleCenter = _cap.center;
    }

    void Update()
    {
        // ввод
        _rawInput.x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        _rawInput.y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        if (_rawInput.sqrMagnitude > 1f) _rawInput.Normalize();

        if (Input.GetKeyDown(KeyCode.Space)) TryJump();
    }

    void FixedUpdate()
    {
        if (!cameraPivot) return;

        float dt = Time.fixedDeltaTime;

        // Повернём игрока по yaw камеры (опционально)
        if (rotateToCameraYaw > 0.01f)
        {
            Vector3 camF = cameraPivot.forward; camF.y = 0; if (camF.sqrMagnitude > 1e-4f) camF.Normalize();
            if (camF.sqrMagnitude > 0.5f)
            {
                Quaternion want = Quaternion.LookRotation(camF, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rotateToCameraYaw * dt);
            }
        }

        GroundCheck();

        // управление отключено во время шага
        if (!_controlEnabled) return;

        // Камерное направление ввода
        Vector3 camForward = cameraPivot.forward; camForward.y = 0; camForward.Normalize();
        Vector3 camRight = cameraPivot.right; camRight.y = 0; camRight.Normalize();

        Vector3 wishDir = (camForward * _rawInput.y + camRight * _rawInput.x);
        if (wishDir.sqrMagnitude > 1e-6f) wishDir.Normalize();

        // 1) Если впереди препятствие — пробуем шагнуть через сенсор
        if (_grounded && wishDir.sqrMagnitude > 1e-6f && !_stepping)
        {
            // проверяем, заблокирован ли ближайший путь капсульным кастом вперёд
            if (BlockedAhead(wishDir, out RaycastHit hit))
            {
                // какой сенсор соответствует этому направлению?
                StepSensor dirSensor = PickSensorForDirection(wishDir);

                // если наверху свободно — запускаем подъём
                if (dirSensor != null && !dirSensor.IsBlocked)
                {
                    StartCoroutine(StepUpRoutine(wishDir));
                    return;
                }
                else
                {
                    // наверху занято — режем вектор: убираем компонент вдоль заблокированного направления
                    wishDir = CutDirectionComponent(wishDir, hit.normal);
                    // если почти ноль — стоим
                    if (wishDir.sqrMagnitude < 1e-4f) wishDir = Vector3.zero;
                }
            }
        }

        // 2) Движение по плоскости (скорость к целевой)
        Vector3 vel = _rb.velocity;
        Vector3 horizVel = new Vector3(vel.x, 0, vel.z);
        Vector3 target = wishDir * moveSpeed;
        float accel = _grounded ? acceleration : acceleration * Mathf.Clamp01(airControl);
        horizVel = Vector3.MoveTowards(horizVel, target, accel * dt);
        _rb.velocity = new Vector3(horizVel.x, vel.y, horizVel.z);
    }

    // --- Шаг вверх ---
    IEnumerator StepUpRoutine(Vector3 worldDir)
    {
        _stepping = true;
        _controlEnabled = false;

        // временно отключим физику, чтобы переместить безопасно
        bool oldKinematic = _rb.isKinematic;
        _rb.isKinematic = true;

        Vector3 start = transform.position;
        Vector3 dest = start + Vector3.up * stepHeight + worldDir.normalized * stepForwardNudge;

        // финальная проверка на свободное место капсулой (подстраховка)
        if (!CapsuleFreeAtWorld(dest))
        {
            // попытка чуть сдвинуться назад (если край занят)
            Vector3 fallback = start + Vector3.up * stepHeight;
            if (!CapsuleFreeAtWorld(fallback))
            {
                // никуда нельзя — отмена шага
                _rb.isKinematic = oldKinematic;
                _controlEnabled = true;
                _stepping = false;
                yield break;
            }
            dest = fallback;
        }

        if (stepLiftDuration <= 0.001f)
        {
            transform.position = dest;
        }
        else
        {
            float t = 0f;
            while (t < stepLiftDuration)
            {
                t += Time.fixedDeltaTime;
                float k = Mathf.Clamp01(t / stepLiftDuration);
                transform.position = Vector3.Lerp(start, dest, k);
                yield return new WaitForFixedUpdate();
            }
            transform.position = dest;
        }

        _rb.isKinematic = oldKinematic;

        // сброс нежелательной вертикальной скорости
        if (_rb.velocity.y < 0f)
        {
            var v = _rb.velocity; v.y = 0f; _rb.velocity = v;
        }

        // маленький снэп вниз, чтобы точно встать на плоскость
        SnapDownSmall();

        _controlEnabled = true;
        _stepping = false;
    }

    // --- Проверки и утилиты ---
    bool BlockedAhead(Vector3 dir, out RaycastHit hit)
    {
        // капсульный каст вперёд на небольшую дистанцию (радиус + небольшой шаг)
        float probe = capsuleRadius + 0.1f;
        GetCapsuleEnds(out Vector3 a, out Vector3 b, out float r);
        return Physics.CapsuleCast(a, b, r, dir, out hit, probe, solidMask, QueryTriggerInteraction.Ignore);
    }

    StepSensor PickSensorForDirection(Vector3 worldDir)
    {
        // выбираем сенсор по доминирующему направлению в системе игрока (после поворота к камере это совпадает)
        Vector3 f = transform.forward; f.y = 0; f.Normalize();
        Vector3 r = transform.right; r.y = 0; r.Normalize();

        float df = Vector3.Dot(worldDir, f);
        float dr = Vector3.Dot(worldDir, r);

        if (Mathf.Abs(df) >= Mathf.Abs(dr))
            return (df >= 0f) ? sensorForward : sensorBack;
        else
            return (dr >= 0f) ? sensorRight : sensorLeft;
    }

    Vector3 CutDirectionComponent(Vector3 wishDir, Vector3 hitNormal)
    {
        // Проецируем желаемый вектор на плоскость стены, чтобы «соскользнуть» вдоль
        Vector3 slide = Vector3.ProjectOnPlane(wishDir, hitNormal);
        // Если слайд очень маленький — стоп
        if (slide.sqrMagnitude < 1e-4f) return Vector3.zero;
        return slide.normalized;
    }

    void TryJump()
    {
        if (!_controlEnabled) return;

        bool can = _grounded || (Time.time - _lastGroundTime) <= coyoteTime;
        if (!can) return;

        var v = _rb.velocity; v.y = jumpSpeed; _rb.velocity = v;
        _grounded = false;
    }

    void GroundCheck()
    {
        _grounded = false;

        GetCapsuleEnds(out Vector3 a, out Vector3 b, out float r);
        if (Physics.CapsuleCast(a, b, r * 0.98f, Vector3.down,
            out RaycastHit h, groundCheckDistance, solidMask, QueryTriggerInteraction.Ignore))
        {
            _grounded = true;
            _lastGroundTime = Time.time;

            // микроснап вниз, если висим в щёлочке
            float d = h.distance - 0.02f;
            if (d > 0f && d < groundCheckDistance)
                _rb.MovePosition(_rb.position + Vector3.down * d);

            // убираем отрицательную вертикаль при касании
            if (_rb.velocity.y < 0f)
            {
                var v = _rb.velocity; v.y = 0f; _rb.velocity = v;
            }
        }
    }

    void SnapDownSmall()
    {
        // с верхней точки капсулы луч вниз на stepHeight + запас
        float half = Mathf.Max(0f, capsuleHeight * 0.5f - capsuleRadius);
        Vector3 center = transform.TransformPoint(capsuleCenter);
        Vector3 top = center + Vector3.up * (half - 0.01f);

        if (Physics.Raycast(top, Vector3.down, out RaycastHit h, stepHeight + 0.4f, solidMask, QueryTriggerInteraction.Ignore))
        {
            float targetCenterY = h.point.y + capsuleRadius + (capsuleCenter.y - capsuleRadius);
            Vector3 pos = transform.position;
            pos.y = targetCenterY - capsuleCenter.y; // перевести центр в позицию
            transform.position = pos;
        }
    }

    bool CapsuleFreeAtWorld(Vector3 worldPosition)
    {
        // проверяем наше положение, если центр капсулы будет в worldPosition (в координатах мира)
        Vector3 oldPos = transform.position;
        // посчитать концы капсулы для гипотетического центра
        Vector3 centerWorld = worldPosition + (transform.rotation * capsuleCenter - (transform.rotation * capsuleCenter));
        // проще: возьмём ends, но сдвинем их по разнице позиции
        GetCapsuleEnds(out Vector3 a, out Vector3 b, out float r);
        Vector3 delta = worldPosition - oldPos;
        a += delta; b += delta;

        return !Physics.CheckCapsule(a, b, r - 0.01f, solidMask, QueryTriggerInteraction.Ignore);
    }

    void GetCapsuleEnds(out Vector3 a, out Vector3 b, out float r)
    {
        r = capsuleRadius;
        float half = Mathf.Max(0f, capsuleHeight * 0.5f - r);

        // ось Y в мировых, центр — как на коллайдере
        Vector3 centerWorld = transform.TransformPoint(capsuleCenter);
        Vector3 axisWorld = transform.up;
        a = centerWorld + axisWorld * (+half);
        b = centerWorld + axisWorld * (-half);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;
        Gizmos.color = Color.cyan;
        GetCapsuleEnds(out var a, out var b, out var r);
        Gizmos.DrawWireSphere(a, r);
        Gizmos.DrawWireSphere(b, r);
        Gizmos.DrawLine(a, b);
    }
}
