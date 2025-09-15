using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RigidbodyVoxelController : MonoBehaviour
{
    public Transform cameraPivot;                 // направление WASD берём по yaw камеры
    public LayerMask solidMask = ~0;             // слои «твёрдых» блоков (воксели/земля/стены)

    [Header("Move")]
    public float moveSpeed = 6f;                 // целевая горизонтальная скорость
    public float acceleration = 25f;             // разгон на земле
    public float airControl = 0.4f;              // 0..1 — управление в воздухе
    public float turnSpeed = 720f;               // поворот тела к направлению движения (град/с)

    [Header("Jump & Gravity")]
    public float jumpSpeed = 6.5f;
    public float coyoteTime = 0.1f;              // «кото-время» после схода с края
    public float groundCheckDistance = 0.2f;     // как далеко вниз ищем землю

    [Header("Step Up (на 1 юнит)")]
    public float stepHeight = 1f;                // высота шага (воксель)
    public float stepProbe = 0.35f;              // расстояние «впереди» для поиска низкой стены
    public float stepUpSpeed = 12f;              // скорость плавного подъёма
    public bool useBoxForStepCheck = true;       // true = «raycastcube» (CheckBox), false = капсула

    [Header("Tuning")]
    public float skin = 0.02f;                   // запас при проверках
    public int maxSlideIters = 2;              // доп. слайд вдоль стен (минимальный)

    Rigidbody rb;
    CapsuleCollider cap;

    Vector2 _input;                              // ось WASD (-1..1)
    bool _jumpPressed;
    bool _grounded;
    float _lastGroundTime;
    float _stepRemaining;                        // сколько ещё подниматься (лифт на +Y)

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cap = GetComponent<CapsuleCollider>();

        // Настройки физики для персонажа
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // не падать набок
        // direction капсулы ожидается Y (по умолчанию у CapsuleCollider)
    }

    void Update()
    {
        // вход
        _input.x = Input.GetAxisRaw("Horizontal");   // A/D
        _input.y = Input.GetAxisRaw("Vertical");     // W/S
        _input = _input.sqrMagnitude > 1 ? _input.normalized : _input;

        if (Input.GetKeyDown(KeyCode.Space)) _jumpPressed = true;
    }

    void FixedUpdate()
    {
        if (!cameraPivot) return;

        float dt = Time.fixedDeltaTime;

        // 1) Граундчек (каст вниз)
        GroundCheck();

        // 2) Прыжок
        if (_jumpPressed)
        {
            if (_grounded || Time.time - _lastGroundTime <= coyoteTime)
            {
                var v = rb.velocity; v.y = jumpSpeed; rb.velocity = v;
                _grounded = false;
            }
            _jumpPressed = false;
        }

        // 3) Горизонтальная цель по камере
        Vector3 fwd = cameraPivot.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = cameraPivot.right; right.y = 0; right.Normalize();
        Vector3 wishDir = (fwd * _input.y + right * _input.x);
        if (wishDir.sqrMagnitude > 1e-4f) wishDir.Normalize();

        // 4) Управление скоростью (vel.xz к целевой)
        Vector3 vel = rb.velocity;
        Vector3 horiz = new Vector3(vel.x, 0, vel.z);
        Vector3 target = wishDir * moveSpeed;
        float accel = (_grounded ? acceleration : acceleration * Mathf.Clamp01(airControl));
        horiz = Vector3.MoveTowards(horiz, target, accel * dt);
        rb.velocity = new Vector3(horiz.x, vel.y, horiz.z);

        // 5) Поворот корпуса по движению
        if (wishDir.sqrMagnitude > 0.001f)
        {
            Quaternion want = Quaternion.LookRotation(wishDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
        }

        // 6) «Шаг» на блок: если впереди упёрлись — проверяем +1 юнит выше
        if (_grounded && wishDir.sqrMagnitude > 0.0001f && _stepRemaining <= 0f)
        {
            TryStartStepUp(wishDir, dt);
        }

        // 7) Выполнить плавный подъём, если он активен
        if (_stepRemaining > 0f)
        {
            float lift = Mathf.Min(_stepRemaining, stepUpSpeed * dt);
            rb.MovePosition(rb.position + Vector3.up * lift);
            // не дать гравитации тянуть вниз во время подъёма
            if (rb.velocity.y < 0f) { var v = rb.velocity; v.y = 0f; rb.velocity = v; }
            _stepRemaining -= lift;
        }

        // 8) Небольшой слайд вдоль стен (устранить стопор)
        if (wishDir.sqrMagnitude > 0.0001f)
            SlideAlongWalls(ref wishDir, ref horiz, dt);
    }

    // ---------- STEP UP ----------
    bool TryStartStepUp(Vector3 wishDir, float dt)
    {
        // 1) Впереди действительно упёрлись?
        if (!ForwardBlocked(wishDir, out RaycastHit wallHit))
            return false;

        // 2) Где окажется центр капсулы, если мы сделаем маленький шаг вперёд?
        //    Берём «будущий центр» чуть дальше края препятствия.
        float pushForward = Mathf.Max(stepProbe, cap.radius + skin + 0.05f);
        Vector3 futureCenterFlat = GetCapsuleWorldCenter() + wishDir * pushForward;

        // 3) Будущий центр ДЛЯ проверки — на +stepHeight выше (воксельная ступень).
        Vector3 climbCenter = futureCenterFlat + Vector3.up * stepHeight;

        // 4) Должно быть свободно ВОКРУГ будущего центра (форма капсулы или «куб»).
        if (!SpaceFreeForCapsuleAt(climbCenter))
            return false;

        // 5) Должна быть опора под ногами (не провалиться).
        //    Каст вниз на высоту шага + небольшой запас, от "верха" будущей капсулы.
        float half = Mathf.Max(0f, cap.height * 0.5f - cap.radius);
        Vector3 topPoint = climbCenter + Vector3.up * (half - 0.01f);
        float rayLen = stepHeight + 0.35f;
        if (!Physics.Raycast(topPoint, Vector3.down, out RaycastHit groundHit, rayLen, solidMask, QueryTriggerInteraction.Ignore))
            return false;

        // Угол «пола» не слишком крутой
        if (Vector3.Angle(groundHit.normal, Vector3.up) > 60f)
            return false;

        // 6) Всё ок — запускаем подъём и ЧУТЬ продвигаем вперёд, чтобы встать на грань.
        _stepRemaining = stepHeight;
        // Мягкий толчок вперёд, чтобы не застрять на вертикальной грани
        rb.MovePosition(rb.position + wishDir * Mathf.Min(0.15f, pushForward * 0.3f));
        return true;
    }

    bool ForwardBlocked(Vector3 dir, out RaycastHit hit)
    {
        // Капсульный КАСТ НИЗКО у ног — ищем «бортик»
        GetCapsuleEnds(out Vector3 a, out Vector3 b, out float r);

        // Слегка опускаем оба конца, чтобы ловить нижнюю грань
        float footDrop = Mathf.Clamp(stepHeight * 0.5f, 0.05f, 0.6f);
        a += Vector3.down * footDrop;
        b += Vector3.down * footDrop;

        float dist = Mathf.Max(stepProbe, r + skin);
        if (Physics.CapsuleCast(a, b, r, dir, out hit, dist, solidMask, QueryTriggerInteraction.Ignore))
        {
            // Отсеиваем «почти пол» (если вдруг луч словил наклон)
            if (Vector3.Angle(hit.normal, Vector3.up) < 20f) return false;
            return true;
        }
        return false;
    }

    bool SpaceFreeForCapsuleAt(Vector3 center)
    {
        GetCapsuleEndsAt(center, out Vector3 a, out Vector3 b, out float r);
        // Чуть «ужмём» радиус на skin, чтобы не цеплять ребро
        float rr = Mathf.Max(0.01f, r - skin);
        return !Physics.CheckCapsule(a, b, rr, solidMask, QueryTriggerInteraction.Ignore);
    }

    bool GroundExistsBelow(Vector3 center)
    {
        // Луч вниз от предполагаемого нового центра на высоту шага + запас
        float rayLen = stepHeight + 0.25f;
        if (Physics.Raycast(center + Vector3.up * 0.05f, Vector3.down, out RaycastHit h, rayLen, solidMask, QueryTriggerInteraction.Ignore))
        {
            // угол не слишком крутой
            if (Vector3.Angle(h.normal, Vector3.up) <= 60f) return true;
        }
        return false;
    }

    // ---------- Ground ----------
    void GroundCheck()
    {
        // Короткий капсульный каст вниз
        GetCapsuleEnds(out Vector3 a, out Vector3 b, out float r);
        if (Physics.CapsuleCast(a, b, r * 0.98f, Vector3.down, out RaycastHit h, groundCheckDistance, solidMask, QueryTriggerInteraction.Ignore))
        {
            _grounded = true;
            _lastGroundTime = Time.time;

            // приклеим чуть-чуть вниз, если висим в микро-зазоре
            float d = h.distance - skin;
            if (d > 0f && d < groundCheckDistance * 0.9f)
                rb.MovePosition(rb.position + Vector3.down * d);
            // обнулить мелкую отрицательную скорость
            if (rb.velocity.y < 0f) { var v = rb.velocity; v.y = 0f; rb.velocity = v; }
        }
        else
        {
            _grounded = false;
        }
    }

    // ---------- Wall slide (минимально) ----------
    void SlideAlongWalls(ref Vector3 wishDir, ref Vector3 horizVel, float dt)
    {
        for (int i = 0; i < maxSlideIters; i++)
        {
            GetCapsuleEnds(out Vector3 a, out Vector3 b, out float r);
            if (Physics.CapsuleCast(a, b, r, wishDir, out RaycastHit h, stepProbe, solidMask, QueryTriggerInteraction.Ignore))
            {
                // проецируем желаемое движение в плоскость стены
                Vector3 n = h.normal;
                if (Vector3.Angle(n, Vector3.up) > 5f) // не пол
                {
                    Vector3 slide = Vector3.ProjectOnPlane(wishDir, n);
                    if (slide.sqrMagnitude < 1e-6f) break;
                    wishDir = slide.normalized;

                    Vector3 target = wishDir * moveSpeed;
                    horizVel = Vector3.MoveTowards(horizVel, target, acceleration * dt);
                    rb.velocity = new Vector3(horizVel.x, rb.velocity.y, horizVel.z);
                }
            }
            else break;
        }
    }

    // ---------- Helpers: капсула/бокс ----------
    Vector3 GetCapsuleWorldCenter()
    {
        return transform.TransformPoint(cap.center);
    }

    void GetCapsuleEnds(out Vector3 a, out Vector3 b, out float r)
    {
        GetCapsuleEndsAt(GetCapsuleWorldCenter(), out a, out b, out r);
    }

    void GetCapsuleEndsAt(Vector3 center, out Vector3 a, out Vector3 b, out float r)
    {
        r = cap.radius;
        float half = Mathf.Max(0f, cap.height * 0.5f - r);
        // ось по направлению коллайдера (должна быть Y)
        Vector3 axis = transform.TransformDirection(cap.direction == 0 ? Vector3.right :
                                                    cap.direction == 1 ? Vector3.up :
                                                                         Vector3.forward);
        a = center + axis * (+half);
        b = center + axis * (-half);
    }

    void GetCapsuleAABB(out Vector3 halfExtents, out Quaternion rot)
    {
        // коробка, которая «накрывает» капсулу по горизонтали и высоте
        float r = cap.radius;
        float halfY = (cap.height * 0.5f) - skin;
        halfExtents = new Vector3(r, halfY, r);
        rot = transform.rotation; // yaw не страшен для вертикальной коробки
    }
}
