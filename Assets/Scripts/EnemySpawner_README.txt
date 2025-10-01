================================================================================
ENEMY SPAWNER - СИСТЕМА СПАВНА ВРАГОВ
================================================================================

ОПИСАНИЕ:
---------
EnemySpawner управляет созданием и спавном врагов в игре.
Поддерживает различные режимы: единоразовый спавн, непрерывный, волнами.

ВАЖНО:
------
⚠️ EnemySpawner должен инициализироваться через GameBootstrap!
⚠️ Метод Initialize() вызывается из GameBootstrap после готовности мира.
⚠️ Это обеспечивает правильную последовательность: Мир → Игрок → Спавнеры


================================================================================
РЕЖИМЫ СПАВНА (Spawn Mode)
================================================================================

1. ON START
   - Спавн при старте игры (один раз)
   - Использует: initialDelay
   - Пример: начальная группа врагов в уровне

2. CONTINUOUS (Непрерывный)
   - Постоянный спавн с интервалом
   - Проверяет maxEnemiesAlive перед спавном
   - Использует: spawnInterval
   - Пример: бесконечный режим выживания

3. WAVE (Волнами)
   - Спавн новой волны после убийства всех врагов
   - Автоматическое увеличение сложности (опционально)
   - Использует: spawnInterval (между волнами)
   - Пример: arena mode, defense mode

4. MANUAL (Ручной)
   - Спавн только по вызову SpawnWave() из кода
   - Полный контроль извне
   - Пример: спавн врагов по триггерам, событиям


================================================================================
РЕЖИМЫ ОБЛАСТИ СПАВНА (Spawn Area Mode)
================================================================================

1. RANDOM IN RADIUS
   - Случайные позиции в радиусе spawnRadius вокруг спавнера
   - Равномерное распределение
   - Проверка безопасности (если включена)

2. SPAWN POINTS
   - Спавн на конкретных точках (массив Transform)
   - Случайный выбор точки из списка
   - Нужно создать пустые GameObject как точки спавна

3. AROUND TARGET
   - Спавн вокруг цели (обычно игрока)
   - Автопоиск PlayerController
   - Враги спавнятся рядом с игроком


================================================================================
НАСТРОЙКА В UNITY
================================================================================

ШАГ 1: СОЗДАТЬ SPAWNER
-----------------------
1. Hierarchy → Create Empty → "EnemySpawner"
2. Поместить в нужную позицию в мире
3. Add Component → EnemySpawner

ШАГ 2: НАСТРОИТЬ ПАРАМЕТРЫ
---------------------------
[Enemy Prefab]
  ✓ Enemy Prefab             - перетащить префаб врага

[Spawn Mode]
  ✓ Spawn Mode               = OnStart/Continuous/Wave/Manual

[Spawn Count]
  ✓ Enemies Per Spawn        = 3    - врагов за раз
  ✓ Max Enemies Alive        = 10   - макс одновременно

[Spawn Area]
  ✓ Area Mode                = RandomInRadius/SpawnPoints/AroundTarget
  ✓ Spawn Radius             = 20   - радиус спавна
  ✓ Spawn Points             = []   - массив точек (для SpawnPoints режима)

[Timing]
  ✓ Spawn Interval           = 30   - интервал между спавнами (сек)
  ✓ Initial Delay            = 3    - задержка перед первым спавном

[Safe Spawn Check]
  ✓ Check Safe Spawn         = true - проверять безопасность
  ✓ Min Spawn Height         = 10   - минимальная высота
  ✓ Max Spawn Height         = 100  - максимальная высота

[Wave System]
  ✓ Increase Per Wave        = false - увеличивать сложность
  ✓ Enemies Increase Per Wave = 1   - +N врагов каждую волну
  ✓ Max Enemies Per Wave     = 20   - максимум в волне


================================================================================
ПРИМЕРЫ НАСТРОЕК
================================================================================

ПРИМЕР 1: АРЕНА (Wave Mode)
----------------------------
Spawn Mode:                 Wave
Enemies Per Spawn:          5
Max Enemies Alive:          50
Spawn Interval:             10 сек
Increase Per Wave:          true
Enemies Increase Per Wave:  2

Результат: Волна 1 = 5 врагов, Волна 2 = 7, Волна 3 = 9...


ПРИМЕР 2: SURVIVAL (Continuous)
--------------------------------
Spawn Mode:                 Continuous
Enemies Per Spawn:          3
Max Enemies Alive:          15
Spawn Interval:             20 сек
Area Mode:                  Around Target

Результат: Каждые 20 сек спавнится 3 врага вокруг игрока,
           но не больше 15 одновременно


ПРИМЕР 3: ОХРАНА БАЗЫ (OnStart + SpawnPoints)
----------------------------------------------
Spawn Mode:                 OnStart
Enemies Per Spawn:          8
Area Mode:                  Spawn Points
Spawn Points:               [Point1, Point2, Point3, Point4]

Результат: При старте на 4 точках появляется по 2 врага


ПРИМЕР 4: УПРАВЛЕНИЕ ИЗ КОДА (Manual)
--------------------------------------
Spawn Mode:                 Manual

Из другого скрипта:
  public EnemySpawner spawner;
  
  void OnTriggerEnter(Collider other)
  {
      spawner.SpawnWave();  // Спавн при входе в триггер
  }


================================================================================
НАСТРОЙКА SPAWN POINTS
================================================================================

1. Create Empty → "SpawnPoint1"
2. Разместить в нужной позиции
3. Повторить для всех точек
4. В EnemySpawner:
   - Spawn Area Mode = Spawn Points
   - Spawn Points → Увеличить Size = 4
   - Перетащить все точки в массив


================================================================================
ПУБЛИЧНЫЕ МЕТОДЫ
================================================================================

Initialize()
------------
Инициализировать спавнер (вызывается из GameBootstrap)

⚠️ ВАЖНО: Вызывается автоматически из GameBootstrap!
Не нужно вызывать вручную при использовании GameBootstrap.

Пример (если не используете GameBootstrap):
  enemySpawner.Initialize();


SetPlayerTarget(Transform player)
----------------------------------
Установить цель (игрока) для всех врагов

Вызывается автоматически из GameBootstrap.

Пример:
  enemySpawner.SetPlayerTarget(player.transform);


SpawnWave()
-----------
Спавнить волну врагов (enemiesPerSpawn штук)

Пример:
  enemySpawner.SpawnWave();


SpawnEnemies(int count)
-----------------------
Спавнить конкретное количество врагов

Пример:
  enemySpawner.SpawnEnemies(5);


StopSpawning()
--------------
Остановить автоматический спавн

Пример:
  enemySpawner.StopSpawning();


ResumeSpawning()
----------------
Возобновить автоматический спавн

Пример:
  enemySpawner.ResumeSpawning();


KillAllEnemies()
----------------
Убить всех заспавненных врагов

Пример:
  enemySpawner.KillAllEnemies();


GetAliveEnemiesCount()
----------------------
Получить количество живых врагов

Пример:
  int count = enemySpawner.GetAliveEnemiesCount();
  Debug.Log($"Врагов живых: {count}");


GetCurrentWave()
----------------
Получить номер текущей волны (для Wave режима)

Пример:
  int wave = enemySpawner.GetCurrentWave();
  Debug.Log($"Волна {wave}");


================================================================================
ИНТЕГРАЦИЯ С ГЕЙМПЛЕЕМ
================================================================================

СПАВН ПО ТРИГГЕРУ:
------------------
public class SpawnTrigger : MonoBehaviour
{
    public EnemySpawner spawner;
    private bool hasTriggered = false;
    
    void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && other.CompareTag("Player"))
        {
            spawner.SpawnWave();
            hasTriggered = true;
        }
    }
}


UI ДЛЯ WAVE РЕЖИМА:
-------------------
public class WaveUI : MonoBehaviour
{
    public EnemySpawner spawner;
    public TMPro.TextMeshProUGUI waveText;
    public TMPro.TextMeshProUGUI enemiesText;
    
    void Update()
    {
        waveText.text = $"Волна: {spawner.GetCurrentWave()}";
        enemiesText.text = $"Врагов: {spawner.GetAliveEnemiesCount()}";
    }
}


АДАПТИВНАЯ СЛОЖНОСТЬ:
---------------------
public class DifficultyManager : MonoBehaviour
{
    public EnemySpawner spawner;
    private int playerKills = 0;
    
    public void OnEnemyKilled()
    {
        playerKills++;
        
        // Каждые 10 убийств - усиливаем спавн
        if (playerKills % 10 == 0)
        {
            spawner.enemiesPerSpawn++;
            spawner.spawnInterval = Mathf.Max(5f, spawner.spawnInterval - 1f);
        }
    }
}


МНОЖЕСТВЕННЫЕ СПАВНЕРЫ:
-----------------------
public class SpawnerManager : MonoBehaviour
{
    public EnemySpawner[] spawners;
    
    public void StartWave(int waveNumber)
    {
        foreach (var spawner in spawners)
        {
            spawner.SpawnWave();
        }
    }
    
    public int GetTotalEnemies()
    {
        int total = 0;
        foreach (var spawner in spawners)
        {
            total += spawner.GetAliveEnemiesCount();
        }
        return total;
    }
}


================================================================================
ПРОВЕРКА БЕЗОПАСНОСТИ СПАВНА
================================================================================

КАК РАБОТАЕТ:
-------------
IsSafeSpawnPosition() проверяет:
  ✓ Позиция в пределах minSpawnHeight - maxSpawnHeight
  ✓ 2 блока свободны для тела врага (Y и Y+1)
  ✓ Твердый блок под ногами (Y-1)
  ✓ Позиция в границах мира

FindSafeYPosition():
  - Ищет подходящую Y координату сверху вниз
  - Находит первую позицию с твердым блоком и свободным местом сверху
  - Спавн на высоте: blockY + 1.5

ЕСЛИ ПОЗИЦИЯ НЕБЕЗОПАСНА:
-------------------------
Спавнер делает несколько попыток (count * 10):
  - Генерирует новую случайную позицию
  - Проверяет безопасность
  - Если безопасна → спавнит
  - Если после всех попыток не найдена → пропускает врага


================================================================================
ОПТИМИЗАЦИЯ
================================================================================

ДЛЯ ЛУЧШЕЙ ПРОИЗВОДИТЕЛЬНОСТИ:
-------------------------------
1. Используйте Object Pooling вместо Instantiate/Destroy
2. Ограничьте maxEnemiesAlive разумным числом (10-20)
3. Не ставьте слишком частый spawnInterval (< 10 сек)
4. Используйте SpawnPoints для контролируемого спавна


ПРИМЕР OBJECT POOL:
-------------------
public class EnemyPool : MonoBehaviour
{
    public GameObject enemyPrefab;
    private Queue<GameObject> pool = new Queue<GameObject>();
    
    public GameObject Get(Vector3 position)
    {
        GameObject enemy;
        if (pool.Count > 0)
        {
            enemy = pool.Dequeue();
            enemy.transform.position = position;
            enemy.SetActive(true);
        }
        else
        {
            enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        }
        return enemy;
    }
    
    public void Return(GameObject enemy)
    {
        enemy.SetActive(false);
        pool.Enqueue(enemy);
    }
}


================================================================================
ВИЗУАЛИЗАЦИЯ В РЕДАКТОРЕ
================================================================================

При выборе EnemySpawner в сцене видны Gizmos:

🟡 Желтая сфера       - Радиус спавна
🟢 Зеленые сферы      - Spawn Points (если используются)
🟢 Зеленые линии      - От спавнера к точкам
🔴 Красный куб        - Позиция спавнера


================================================================================
ЛОГИ В КОНСОЛИ
================================================================================

При работе EnemySpawner:

EnemySpawner [EnemySpawner]: Заспавнено 3 врагов
EnemySpawner [EnemySpawner]: Волна 1 начинается! Врагов: 5
EnemySpawner [EnemySpawner]: Спавн остановлен
EnemySpawner [EnemySpawner]: Все враги уничтожены


При ошибках:

EnemySpawner [EnemySpawner]: Enemy Prefab не указан!
EnemySpawner [EnemySpawner]: Не удалось заспавнить всех врагов. Заспавнено: 2/5
EnemySpawner [EnemySpawner]: VoxelWorld не найден, проверка безопасности отключена


================================================================================
ПРИМЕР СЦЕНЫ
================================================================================

ВАРИАНТ 1: Простой спавнер в центре
------------------------------------
Scene:
├─ EnemySpawner (0, 0, 0)
│  [EnemySpawner]
│    Enemy Prefab: EnemyBotPrefab
│    Spawn Mode: Continuous
│    Spawn Radius: 20

ВАРИАНТ 2: Спавн на точках
---------------------------
Scene:
├─ EnemySpawner
│  [EnemySpawner]
│    Area Mode: SpawnPoints
│    Spawn Points: [Point1, Point2, Point3, Point4]
├─ SpawnPoint1 (10, 50, 10)
├─ SpawnPoint2 (-10, 50, 10)
├─ SpawnPoint3 (10, 50, -10)
└─ SpawnPoint4 (-10, 50, -10)

ВАРИАНТ 3: Волны с увеличением сложности
-----------------------------------------
Scene:
├─ EnemySpawner
   [EnemySpawner]
     Spawn Mode: Wave
     Enemies Per Spawn: 3
     Spawn Interval: 15
     Increase Per Wave: true
     Enemies Increase Per Wave: 2
     Max Enemies Per Wave: 20


================================================================================
СЦЕНАРИИ ИСПОЛЬЗОВАНИЯ
================================================================================

СЦЕНАРИЙ 1: АРЕНА ВЫЖИВАНИЯ
----------------------------
1. Spawn Mode = Wave
2. Increase Per Wave = true
3. Area Mode = RandomInRadius
4. Spawn Radius = 30

Поведение:
  - Волна 1: 3 врага
  - Игрок убивает всех → Волна 2: 5 врагов
  - Игрок убивает всех → Волна 3: 7 врагов
  - И так далее до maxEnemiesPerWave


СЦЕНАРИЙ 2: ОХРАНА БАЗЫ
------------------------
1. Spawn Mode = OnStart
2. Area Mode = SpawnPoints
3. Enemies Per Spawn = 8

Поведение:
  - При старте уровня спавнятся 8 врагов на конкретных точках
  - Статичная охрана базы


СЦЕНАРИЙ 3: БЕСКОНЕЧНЫЙ РЕЖИМ
------------------------------
1. Spawn Mode = Continuous
2. Area Mode = Around Target
3. Spawn Interval = 20
4. Max Enemies Alive = 15

Поведение:
  - Каждые 20 секунд спавнятся враги вокруг игрока
  - Не больше 15 одновременно
  - Постоянное давление на игрока


СЦЕНАРИЙ 4: СКРИПТОВЫЕ СОБЫТИЯ
-------------------------------
1. Spawn Mode = Manual

Код триггера:
  void OnPlayerEnterArea()
  {
      spawner.SpawnEnemies(10); // Массированный спавн
  }


================================================================================
БЕЗОПАСНЫЙ СПАВН
================================================================================

ПРОВЕРКА БЕЗОПАСНОСТИ (checkSafeSpawn = true):
-----------------------------------------------
1. FindSafeYPosition() - УМНЫЙ ПОИСК:
   - Выбирает случайную XZ позицию в радиусе
   - Ищет безопасное место СНИЗУ ВВЕРХ (поднимая Y)
   - Проверяет: твердый блок под ногами + 3 блока свободно сверху
   - Проверяет дистанцию до игрока (minDistanceFromPlayer)
   - Если дошли до maxSpawnHeight и не нашли → возвращает Vector3.zero
   - Тогда выбирается ДРУГАЯ XZ позиция и повторяется поиск

2. IsSafeSpawnPosition():
   - Проверка высоты (min/max)
   - 2 блока свободны (Y, Y+1)
   - Твердый блок под ногами (Y-1)

3. Множественные попытки (maxSpawnAttempts):
   - Если позиция небезопасна → пробует другую XZ
   - Для каждой XZ поднимает Y пока не найдет или дойдет до верха
   - Максимум попыток = maxSpawnAttempts (50 по умолчанию)
   - Логирует количество попыток

4. Защита от спавна рядом с игроком:
   - minDistanceFromPlayer = 10 блоков
   - Враг не заспавнится ближе этой дистанции
   - Игрок защищен от внезапного появления врагов


ОТКЛЮЧЕНИЕ ПРОВЕРКИ:
--------------------
Если checkSafeSpawn = false:
  - Спавн на точной позиции без проверок
  - Враг может заспавниться в воздухе или в блоке
  - Используйте только если уверены в позициях


================================================================================
УПРАВЛЕНИЕ ИЗ КОДА
================================================================================

ОСТАНОВИТЬ СПАВН ПРИ ПОБЕДЕ:
----------------------------
public class GameManager : MonoBehaviour
{
    public EnemySpawner spawner;
    
    void OnPlayerWin()
    {
        spawner.StopSpawning();
        spawner.KillAllEnemies();
    }
}


ИЗМЕНЕНИЕ СЛОЖНОСТИ В РЕАЛЬНОМ ВРЕМЕНИ:
---------------------------------------
public void IncreaseDifficulty()
{
    spawner.enemiesPerSpawn += 2;
    spawner.spawnInterval = Mathf.Max(5f, spawner.spawnInterval - 2f);
    spawner.maxEnemiesAlive += 5;
}


УСЛОВНЫЙ СПАВН:
---------------
public class ConditionalSpawner : MonoBehaviour
{
    public EnemySpawner spawner;
    
    void Update()
    {
        int aliveEnemies = spawner.GetAliveEnemiesCount();
        
        // Спавним только если врагов меньше 5
        if (aliveEnemies < 5)
        {
            spawner.SpawnEnemies(1);
        }
    }
}


================================================================================
ОТЛАДКА
================================================================================

ПРОБЛЕМА: Враги не спавнятся
----------------------------
Проверить:
  ✓ Enemy Prefab назначен
  ✓ Prefab имеет EnemyBot компонент
  ✓ Max Enemies Alive не превышен
  ✓ В консоли нет ошибок
  ✓ Spawn Mode не Manual (или вызывается SpawnWave())


ПРОБЛЕМА: Враги спавнятся в воздухе
------------------------------------
Решение:
  ✓ Включить Check Safe Spawn
  ✓ Проверить Min/Max Spawn Height
  ✓ Убедиться что VoxelWorld.Instance доступен


ПРОБЛЕМА: Враги не спавнятся (все попытки неудачны)
----------------------------------------------------
Причина: Не находит безопасных позиций
Решение:
  ✓ Увеличить Spawn Radius
  ✓ Проверить что в зоне спавна есть земля
  ✓ Уменьшить количество Enemies Per Spawn
  ✓ Использовать Spawn Points с проверенными позициями


ЛОГИРОВАНИЕ ДЛЯ ОТЛАДКИ:
-------------------------
В EnemySpawner.cs раскомментировать Debug.Log:

  Debug.Log($"Попытка спавна в позиции: {spawnPosition}");
  Debug.Log($"Безопасность: {IsSafeSpawnPosition(spawnPosition)}");


================================================================================
ВИЗУАЛЬНАЯ ОТЛАДКА
================================================================================

Scene View при выборе EnemySpawner:
  🟡 Желтая сфера - зона спавна (меняется в реальном времени для Around Target)
  🟢 Зеленые сферы - назначенные Spawn Points
  🔴 Красный куб - позиция спавнера

Game View:
  - Враги появляются с именем: "Enemy_{wave}_{number}"
  - Легко отследить какая волна и порядковый номер


================================================================================
РАСШИРЕННЫЕ ВОЗМОЖНОСТИ
================================================================================

ДОБАВИТЬ РАЗНЫЕ ТИПЫ ВРАГОВ:
-----------------------------
public GameObject[] enemyPrefabs; // Массив разных врагов

void SpawnEnemies(int count)
{
    foreach (...)
    {
        // Случайный префаб из массива
        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        GameObject enemy = Instantiate(prefab, position, rotation);
        ...
    }
}


СПАВН С ЗАДЕРЖКОЙ МЕЖДУ ВРАГАМИ:
---------------------------------
IEnumerator SpawnEnemiesWithDelay(int count, float delayBetween)
{
    for (int i = 0; i < count; i++)
    {
        Vector3 pos = GetSpawnPosition();
        if (IsSafeSpawnPosition(pos))
        {
            Instantiate(enemyPrefab, pos, Quaternion.identity);
        }
        yield return new WaitForSeconds(delayBetween);
    }
}


ЭФФЕКТ ПРИ СПАВНЕ:
------------------
void SpawnEnemies(int count)
{
    ...
    GameObject enemy = Instantiate(enemyPrefab, position, rotation);
    
    // Эффект спавна
    if (spawnEffectPrefab != null)
    {
        Instantiate(spawnEffectPrefab, position, Quaternion.identity);
    }
    ...
}


================================================================================

