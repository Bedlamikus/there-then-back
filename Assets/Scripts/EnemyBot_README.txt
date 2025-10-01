================================================================================
ENEMYBOT - AI ПРОТИВНИК НА БАЗЕ PLAYERCONTROLLER
================================================================================

ОПИСАНИЕ:
---------
EnemyBot - враг-бот, использующий ту же логику движения что и игрок (CharacterController),
но с простым AI вместо ввода игрока.

ОСОБЕННОСТИ:
------------
✅ Использует CharacterController (как игрок)
✅ Та же физика и гравитация
✅ Поддержка анимации (Animator)
✅ Простой AI с 4 состояниями
✅ Автоматический поиск игрока
✅ Визуализация в редакторе (Gizmos)


================================================================================
СОСТОЯНИЯ AI
================================================================================

1. IDLE (Ожидание)
   - Стоит на месте
   - Переходит в Patrol через patrolWaitTime
   - Обнаружив цель → Chase

2. PATROL (Патрулирование)
   - Движется к случайным точкам в радиусе patrolRadius
   - Достигнув точки → ждет patrolWaitTime
   - Обнаружив цель → Chase

3. CHASE (Преследование)
   - Преследует цель (обычно игрока)
   - Приблизившись на attackRange → Attack
   - Если цель ушла далеко → Patrol

4. ATTACK (Атака)
   - Стоит рядом с целью
   - Если цель отошла → Chase
   - TODO: Добавить логику атаки


================================================================================
НАСТРОЙКА В UNITY
================================================================================

ШАГ 1: СОЗДАТЬ ВРАГА
--------------------
1. Duplicate префаб игрока или создать новый GameObject
2. Назвать: "EnemyBot"
3. Убрать компонент PlayerController
4. Add Component → EnemyBot
5. Add Component → CharacterController (если нет)

ШАГ 2: НАСТРОИТЬ ПАРАМЕТРЫ
---------------------------
[Target]
  ✓ Target               - (опционально) ссылка на цель
  ✓ Auto Find Player     = true - автопоиск игрока

[Movement]
  ✓ Move Speed           = 4    - скорость движения
  ✓ Turn Speed           = 5    - скорость поворота
  ✓ Turn Threshold       = 0.1  - минимум для поворота

[AI Behavior]
  ✓ Detection Range      = 20   - дальность обнаружения
  ✓ Attack Range         = 2    - дальность атаки
  ✓ Patrol Radius        = 10   - радиус патруля от стартовой позиции
  ✓ Patrol Wait Time     = 2    - время ожидания на точке
  ✓ Min Patrol Distance  = 3    - минимальная дистанция между точками

[Gravity]
  ✓ Gravity              = -9.81
  ✓ Ground Check Distance = 0.2

[Animation]
  ✓ Animator             - ссылка на Animator
  ✓ Speed Parameter      = "Speed"
  ✓ IsGrounded Parameter = "IsGrounded"


================================================================================
КАК ЭТО РАБОТАЕТ
================================================================================

АВТОПОИСК ИГРОКА:
-----------------
При Start():
  └─ autoFindPlayer = true?
     └─ FindObjectOfType<PlayerController>()
        └─ target = player.transform

ЦИКЛ AI:
--------
Update():
  ├─ UpdateAI()         → определяет состояние и поведение
  ├─ HandleMovement()   → движение на основе состояния
  └─ UpdateAnimation()  → обновление параметров аниматора

ПАТРУЛИРОВАНИЕ:
---------------
SetNewPatrolTarget():
  1. Генерирует случайную точку в радиусе patrolRadius от startPosition
  2. Проверяет минимальную дистанцию (minPatrolDistance)
  3. Бот движется к точке
  4. Достигнув точки → ждет patrolWaitTime секунд
  5. Генерирует новую точку

ОБНАРУЖЕНИЕ:
------------
Каждый кадр проверяет расстояние до цели:
  - Если distance <= detectionRange → Chase
  - Если distance <= attackRange → Attack
  - Если distance > detectionRange * 1.5 → Patrol (потерял цель)


================================================================================
ВИЗУАЛИЗАЦИЯ В РЕДАКТОРЕ
================================================================================

При выборе EnemyBot в сцене видны Gizmos:

🟡 Желтая сфера       - Detection Range (дальность обнаружения)
🔴 Красная сфера      - Attack Range (дальность атаки)
🔵 Синяя сфера        - Patrol Radius (радиус патрулирования)
🟢 Зеленая сфера      - Текущая точка патруля (в режиме Patrol)
🔴 Красная линия      - Линия к цели (в режиме Chase/Attack)


================================================================================
ПУБЛИЧНЫЕ МЕТОДЫ
================================================================================

SetTarget(Transform newTarget)
------------------------------
Установить новую цель для преследования

Пример:
  enemyBot.SetTarget(player.transform);


GetCurrentState()
-----------------
Получить текущее состояние AI

Пример:
  var state = enemyBot.GetCurrentState();
  if (state == AIState.Chase)
      Debug.Log("Враг преследует!");


ForceState(AIState newState)
-----------------------------
Принудительно установить состояние AI

Пример:
  enemyBot.ForceState(AIState.Attack);


FindPlayer()
------------
Найти игрока в сцене и установить как цель

Пример:
  enemyBot.FindPlayer();


================================================================================
ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
================================================================================

ПРИМЕР 1: ПРОСТОЙ ВРАГ В СЦЕНЕ
-------------------------------
1. Создать GameObject → "Enemy"
2. Add Component → EnemyBot
3. Add Component → CharacterController
4. Настроить параметры
5. Запустить игру → враг автоматически найдет игрока и начнет патрулировать

ПРИМЕР 2: СПАВН ВРАГОВ ИЗ КОДА
-------------------------------
public GameObject enemyPrefab;

void SpawnEnemy(Vector3 position)
{
    GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
    EnemyBot bot = enemy.GetComponent<EnemyBot>();
    
    // Настройка
    bot.moveSpeed = 5f;
    bot.detectionRange = 30f;
    bot.SetTarget(player.transform);
}

ПРИМЕР 3: ГРУППА ВРАГОВ
------------------------
public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public int enemyCount = 5;
    public float spawnRadius = 20f;
    
    void Start()
    {
        for (int i = 0; i < enemyCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        }
    }
}


================================================================================
РАЗЛИЧИЯ С PLAYERCONTROLLER
================================================================================

ЧТО ИСПОЛЬЗУЕТ ТАК ЖЕ:
-----------------------
✓ CharacterController - та же физика
✓ Gravity и Ground Check - та же гравитация
✓ Animator - те же анимации
✓ Turn Speed - та же скорость поворота

ЧТО ОТЛИЧАЕТСЯ:
---------------
❌ Не использует GlobalEvents (PlayerMove, PlayerJump)
❌ Не использует AutoSpawnService
❌ Не нужен cameraPivot (движется к цели напрямую)
❌ Нет Jump (можно добавить при необходимости)
✅ Добавлен AI с состояниями
✅ Добавлено патрулирование
✅ Добавлено преследование
✅ Добавлено обнаружение цели


================================================================================
ИНТЕГРАЦИЯ С ГЕЙМПЛЕЕМ
================================================================================

ДОБАВИТЬ ЗДОРОВЬЕ:
------------------
public class EnemyBot : MonoBehaviour
{
    public int health = 100;
    
    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            Die();
        }
    }
    
    void Die()
    {
        // Анимация смерти, дроп лута и т.д.
        Destroy(gameObject, 2f);
    }
}

ДОБАВИТЬ АТАКУ:
---------------
В состоянии Attack:

    case AIState.Attack:
        // Атака раз в N секунд
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown)
        {
            PerformAttack();
            attackTimer = 0f;
        }
        break;

void PerformAttack()
{
    if (target != null)
    {
        // Нанести урон игроку
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
    }
}


================================================================================
ОПТИМИЗАЦИЯ
================================================================================

ДЛЯ БОЛЬШОГО КОЛИЧЕСТВА ВРАГОВ:
--------------------------------
1. Используйте NavMeshAgent вместо CharacterController (более оптимален)
2. Уменьшите частоту обновления AI (не каждый кадр)
3. Используйте пулы объектов вместо Instantiate/Destroy
4. Отключайте врагов далеко от игрока

ПРИМЕР ОПТИМИЗАЦИИ ОБНОВЛЕНИЯ:
-------------------------------
private float aiUpdateInterval = 0.2f;  // Обновлять AI раз в 0.2 сек
private float lastAIUpdate = 0f;

void Update()
{
    // AI обновляется не каждый кадр
    if (Time.time - lastAIUpdate >= aiUpdateInterval)
    {
        UpdateAI();
        lastAIUpdate = Time.time;
    }
    
    // Движение каждый кадр для плавности
    HandleMovement();
    UpdateAnimation();
}


================================================================================
РАСШИРЕНИЕ ФУНКЦИОНАЛА
================================================================================

ДОБАВИТЬ ПРЫЖКИ:
----------------
Скопировать логику из PlayerController:

    [Header("Jump")]
    public float jumpHeight = 3.5f;
    public float coyoteTime = 0.1f;
    private bool shouldJump = false;

В HandleMovement():
    if (shouldJump && (_grounded || Time.time - _lastGroundTime <= coyoteTime))
    {
        _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        shouldJump = false;
    }

ДОБАВИТЬ РАЗНЫЕ ТИПЫ ВРАГОВ:
-----------------------------
Создать дочерние классы:

public class FastEnemyBot : EnemyBot
{
    void Start()
    {
        base.Start();
        moveSpeed = 8f;        // Быстрый
        detectionRange = 30f;  // Видит далеко
    }
}

public class TankEnemyBot : EnemyBot
{
    void Start()
    {
        base.Start();
        moveSpeed = 2f;        // Медленный
        attackRange = 5f;      // Атакует издалека
    }
}


================================================================================
ОТЛАДКА
================================================================================

ЛОГИРОВАНИЕ СОСТОЯНИЙ:
----------------------
private AIState lastState;

void UpdateAI()
{
    // ... логика AI ...
    
    if (currentState != lastState)
    {
        Debug.Log($"[{name}] Смена состояния: {lastState} → {currentState}");
        lastState = currentState;
    }
}

ПОКАЗАТЬ ИНФОРМАЦИЮ В GUI:
--------------------------
void OnGUI()
{
    Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 3);
    GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 200, 20), 
              $"State: {currentState}");
}


================================================================================

