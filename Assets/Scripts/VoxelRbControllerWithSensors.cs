using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class VoxelRbControllerWithSensors : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Пивот/камера, чей Yaw задаёт направление WASD")]
    public Transform cameraPivot;
    [Tooltip("Сенсор на высоте +1, смотрит вперёд, даёт IsBlocked")]
    public StepSensor sensorForward;

    [Header("Layers & Probes")]
    [Tooltip("Слои вокселей/препятствий")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("Слой земли (можно тот же, что obstacleMask)")]
    public LayerMask groundMask = ~0;
    [Tooltip("Дистанция проверки упора перед ногами для шага/скольжения")]
    public float stepProbeDistance = 0.6f;
    [Tooltip("Радиус нижней сферы для проверки упора")]
    public float footProbeRadius = 0.25f;
    [Tooltip("Вертикальный сдвиг зонда ног от низа капсулы")]
    public float footProbeLift = 0.05f;

    [Header("Move")]
    [Tooltip("Целевая горизонтальная скорость")]
    public float moveSpeed = 6f;
    [Tooltip("Разгон до целевой скорости (ускорение)")]
    public float acceleration = 30f;
    [Range(0f, 1f)]
    [Tooltip("Контроль в воздухе (0..1)")]
    public float airControl = 0.5f;
    [Tooltip("Скорость поворота yaw к камере (0 — не крутить)")]
    public float rotateToCameraYaw = 720f;

    [Header("Step Up")]
    [Tooltip("Высота подъёма (воксель)")]
    public float stepHeight = 1f;
    [Tooltip("Насколько выносим вперёд при подъёме, чтобы не скатиться")]
    public float stepForwardNudge = 0.15f;
    [Tooltip("Длительность плавного подъёма (сек). 0 — телепорт")]
    public float stepLiftDuration = 0.08f;

    // internal
    Vector2 _rawInput;
    Rigidbody _rb;
    CapsuleCollider _capsule;
    bool _isStepping;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _capsule = GetComponent<CapsuleCollider>();
    }

    void Update()
    {
        // Ввод WASD
        _rawInput.x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        _rawInput.y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        if (_rawInput.sqrMagnitude > 1f) _rawInput.Normalize();

        // Поворот к yaw камеры (только по Y)
        if (rotateToCameraYaw > 0f && cameraPivot != null)
        {
            var camYaw = Quaternion.Euler(0f, cameraPivot.rotation.eulerAngles.y, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                camYaw,
                rotateToCameraYaw * Time.deltaTime
            );
        }
    }

    void FixedUpdate()
    {
        // 1) Камера-относительное движение на плоскости XZ
        Vector3 moveDir = GetCameraRelativeMoveOnPlane(_rawInput, out Vector3 facingDir);
        bool grounded = CheckGrounded();

        // 2) Проверка упора "перед ногами" для шага/антизалипания
        bool lowHit = false;
        RaycastHit hit = new RaycastHit();
        {
            float bottomY = transform.position.y + _capsule.center.y - _capsule.height * 0.5f + _capsule.radius;
            Vector3 footCenter = new Vector3(transform.position.x, bottomY + footProbeLift, transform.position.z);
            lowHit = Physics.SphereCast(
                footCenter,
                footProbeRadius,
                facingDir == Vector3.zero ? transform.forward : facingDir, // на случай нулевого ввода
                out hit,
                stepProbeDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );
        }

        // 3) WALL SLIDE / ANTI-STICK:
        // если упёрлись и сенсор запрещает шаг — скользим вдоль стены и не давим внутрь
        if (lowHit && sensorForward != null && sensorForward.IsBlocked)
        {
            Vector3 n = hit.normal;

            // убрать текущую горизонтальную скорость "в стену"
            Vector3 vel = _rb.velocity;
            Vector3 horizVel = Vector3.ProjectOnPlane(vel, Vector3.up);
            float into = Vector3.Dot(horizVel, -n); // положительно, если летим в стену
            if (into > 0f)
            {
                _rb.velocity = vel + n * into; // гасим компоненту в стену
            }

            // проецируем ввод на плоскость стены — скольжение вдоль препятствия
            Vector3 slidDir = Vector3.ProjectOnPlane(moveDir, n);
            moveDir = slidDir.sqrMagnitude > 1e-6f ? slidDir.normalized : Vector3.zero;
        }

        // 4) Разгон к целевой скорости AddForce (по плоскости)
        Vector3 vNow = _rb.velocity;
        Vector3 vHoriz = Vector3.ProjectOnPlane(vNow, Vector3.up);
        Vector3 desired = moveDir * moveSpeed;
        float ctrl = grounded ? 1f : airControl;

        if (lowHit && sensorForward != null && sensorForward.IsBlocked && desired.sqrMagnitude < 1e-6f)
        {
            // Нет слайда — слегка демпфируем горизонт, чтобы быстро "отпускало" от стены
            _rb.AddForce(-vHoriz * (acceleration * 0.5f), ForceMode.Acceleration);
        }
        else
        {
            Vector3 accel = (desired - vHoriz) * (acceleration * ctrl);
            _rb.AddForce(accel, ForceMode.Acceleration);
        }

        // 5) Шаг на воксель (телепорт/плавно) — только если упёрлись и сенсор свободен
        if (!_isStepping && grounded && moveDir.sqrMagnitude > 0.0001f)
        {
            if (lowHit && sensorForward != null && !sensorForward.IsBlocked)
            {
                Vector3 stepForward = GetFacingFromMove(moveDir);
                Vector3 stepDelta = Vector3.up * stepHeight + stepForward * stepForwardNudge;

                // убрать возможное прижатие вниз перед подъёмом
                if (_rb.velocity.y < 0f)
                {
                    Vector3 v = _rb.velocity; v.y = 0f; _rb.velocity = v;
                }

                StartCoroutine(DoStepLift(stepDelta));
            }
        }
    }

    Vector3 GetCameraRelativeMoveOnPlane(Vector2 input, out Vector3 facingDir)
    {
        if (cameraPivot == null)
        {
            var m = new Vector3(input.x, 0f, input.y);
            if (m.sqrMagnitude > 1f) m.Normalize();
            facingDir = m.sqrMagnitude > 0f ? m : transform.forward;
            return m;
        }

        Vector3 fwd = cameraPivot.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = cameraPivot.right; right.y = 0f; right.Normalize();

        Vector3 move = right * input.x + fwd * input.y;
        if (move.sqrMagnitude > 1f) move.Normalize();

        facingDir = move.sqrMagnitude > 0.0001f ? move : fwd;
        return move;
    }

    bool CheckGrounded()
    {
        // короткий сферакаст вниз от низа капсулы
        float bottomY = transform.position.y + _capsule.center.y - _capsule.height * 0.5f + _capsule.radius;
        Vector3 start = new Vector3(transform.position.x, bottomY + 0.02f, transform.position.z);
        return Physics.SphereCast(
            start,
            _capsule.radius * 0.95f,
            Vector3.down,
            out _,
            0.06f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    IEnumerator DoStepLift(Vector3 worldDelta)
    {
        _isStepping = true;

        if (stepLiftDuration <= 0f)
        {
            _rb.position += worldDelta; // мгновенный подъём
        }
        else
        {
            Vector3 start = _rb.position;
            Vector3 end = start + worldDelta;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.fixedDeltaTime / stepLiftDuration;
                _rb.MovePosition(Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t)));
                yield return new WaitForFixedUpdate();
            }
            _rb.MovePosition(end);
        }

        _isStepping = false;
    }

    // Вспомогалка: получить «вперёд» из moveDir, fallback — forward персонажа/мир Z+
    Vector3 GetFacingFromMove(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude > 1e-6f) return moveDir.normalized;
        var fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude > 1e-6f) return fwd.normalized;
        return Vector3.forward;
    }
}
