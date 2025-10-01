================================================================================
GAME BOOTSTRAP - ТОЧКА ВХОДА В ИГРУ
================================================================================

НАЗНАЧЕНИЕ:
-----------
GameBootstrap управляет последовательностью инициализации игры:
  1. Генерация/загрузка мира (VoxelWorld)
  2. Спавн игрока из префаба в безопасной позиции


================================================================================
НАСТРОЙКА В UNITY
================================================================================

ШАГ 1: СОЗДАТЬ ПУСТОЙ GAMEOBJECT
---------------------------------
1. Hierarchy → Right Click → Create Empty
2. Назвать: "GameBootstrap"
3. Position: (0, 0, 0)


ШАГ 2: ДОБАВИТЬ КОМПОНЕНТ GameBootstrap
----------------------------------------
1. Выбрать GameBootstrap GameObject
2. Inspector → Add Component → GameBootstrap
3. Настроить параметры:

   [References]
   ✓ Player Prefab        - перетащить префаб игрока
   ✓ Voxel World          - (опционально) перетащить VoxelWorld из сцены
   ✓ Camera Controller    - (опционально) перетащить CameraController из сцены
   ✓ Enemy Spawners       - (опционально) массив спавнеров врагов
   ✓ Auto Find Spawners   = true - автопоиск спавнеров в сцене
   
   [Settings]
   ✓ Wait For World Ready          = true   - ждать готовности мира
   ✓ Max Wait Time                 = 60     - таймаут ожидания (сек)
   ✓ Disable Player Until Ready    = true   - отключить игрока пока мир не готов
   ✓ Initialize Camera             = true   - инициализировать камеру после спавна
   
   [Player Save]
   ✓ Save Player Position          = true   - сохранять позицию игрока
   ✓ Player Save Interval          = 5      - интервал автосохранения (сек)


ШАГ 3: УБРАТЬ ИГРОКА ИЗ СЦЕНЫ
------------------------------
Если в сцене уже есть игрок:
1. Удалить его из сцены (или отключить)
2. Создать из него префаб:
   - Перетащить в папку Prefabs
   - Назвать: "Player"
3. Перетащить префаб в поле "Player Prefab" в GameBootstrap


ШАГ 4: НАСТРОИТЬ EXECUTION ORDER (ВАЖНО!)
------------------------------------------
GameBootstrap должен запускаться РАНЬШЕ других скриптов:

1. Edit → Project Settings → Script Execution Order
2. Добавить GameBootstrap
3. Установить значение: -100 (раньше Default Time)
4. Apply

Альтернативно: уже настроено в .meta файле (executionOrder: -100)


================================================================================
КАК ЭТО РАБОТАЕТ
================================================================================

ПОСЛЕДОВАТЕЛЬНОСТЬ ЗАПУСКА:
---------------------------
1. GameBootstrap.Start()
   ↓
2. InitializeGame() корутина
   ↓
3. Поиск VoxelWorld в сцене (если не указан)
   ↓
4. Ожидание VoxelWorld.IsWorldReady = true
   ↓
5. SpawnPlayer() корутина
   ↓
6. Получение безопасной позиции: VoxelWorld.GetSafeSpawnPosition()
   ↓
7. Instantiate(playerPrefab)
   ↓
8. InitializeCamera() корутина
   ↓
9. Поиск CameraController в сцене (если не указан)
   ↓
10. Связывание камеры с игроком
   ↓
11. Создание CameraPivot (если нужно)
   ↓
12. ResetCamera() для инициализации позиции
   ↓
13. InitializeEnemySpawners()
   ↓
14. Поиск всех EnemySpawner в сцене (если autoFindSpawners)
   ↓
15. Передача ссылки на игрока всем спавнерам
   ↓
16. Игрок, камера и враги готовы к игре!


БЕЗОПАСНАЯ ПОЗИЦИЯ СПАВНА:
--------------------------
VoxelWorld.GetSafeSpawnPosition() ищет:
  ✓ Центр мира по X и Z
  ✓ Поиск сверху вниз
  ✓ Твердый блок снизу
  ✓ Минимум 3 блока свободного пространства сверху
  ✓ Спавн на высоте: blockY + 1.5

Если не найдена → спавн на высоте 80 в центре мира


ИНИЦИАЛИЗАЦИЯ КАМЕРЫ:
---------------------
InitializeCamera() выполняет:
  ✓ Поиск CameraController в сцене (если не указан)
  ✓ Установка playerTransform в CameraController
  ✓ Создание CameraPivot для игрока (если отсутствует)
  ✓ ResetCamera() для правильной начальной позиции
  ✓ Камера готова следовать за игроком

Примечание: CameraPivot нужен для PlayerController.cameraPivot


ИНИЦИАЛИЗАЦИЯ СПАВНЕРОВ ВРАГОВ:
--------------------------------
InitializeEnemySpawners() выполняет:
  1. Автопоиск всех EnemySpawner в сцене (если autoFindSpawners)
  2. Для каждого спавнера:
     → spawner.SetPlayerTarget(player) - передает ссылку на игрока
     → spawner.Initialize() - запускает спавнер
  3. Враги получают игрока как цель и начинают спавниться

⚠️ ВАЖНО: 
  - EnemySpawner НЕ должен иметь Start()
  - Инициализация происходит через Initialize()
  - Это гарантирует что:
    ✓ Мир уже готов (IsWorldReady = true)
    ✓ Игрок уже создан
    ✓ VoxelWorld доступен для проверки позиций
    ✓ Враги не спавнятся рядом с игроком (minDistanceFromPlayer)


СОХРАНЕНИЕ ПОЗИЦИИ ИГРОКА:
---------------------------
SpawnPlayer() проверяет сохранение:
  1. Проверяет наличие "SaveData_PlayerPosition" в PlayerPrefs
  2. Если найдено:
     → Загружает позицию и ротацию
     → Проверяет безопасность позиции (IsPositionSafe)
     → Если безопасна - спавнит там
     → Если небезопасна - использует GetSafeSpawnPosition()
  3. Если не найдено:
     → Использует GetSafeSpawnPosition()

Автосохранение:
  ✓ Каждые 5 секунд (playerSaveInterval)
  ✓ При выходе из игры (OnApplicationQuit)
  ✓ Сохраняется: позиция (x,y,z) и ротация

Проверка безопасности:
  ✓ Позиция в границах мира
  ✓ Есть твердый блок под ногами
  ✓ Минимум 2 блока свободного места над головой


ЭКРАН ЗАГРУЗКИ:
---------------
Во время инициализации на экране отображается:
  - "Генерация мира..."  → пока VoxelWorld генерируется
  - "Спавн игрока..."    → после готовности мира
  - Исчезает после полной инициализации


================================================================================
ЛОГИ В КОНСОЛИ
================================================================================

При запуске игры вы увидите:

=== GameBootstrap: Начало инициализации игры ===
GameBootstrap: Поиск VoxelWorld...
GameBootstrap: VoxelWorld найден: VoxelWorld
GameBootstrap: Ожидание готовности мира...

VoxelWorld: Первый запуск игры - генерируем новый мир
VoxelWorld: Начинаем постепенную генерацию (5x5 чанков)
VoxelWorld: Постепенная генерация завершена
VoxelWorld: Инициализация завершена (25 чанков), мир готов!

GameBootstrap: Мир готов! (время ожидания: 2.45 сек)
GameBootstrap: Спавн игрока...
GameBootstrap: Позиция спавна: (40.5, 67.5, 40.5)
GameBootstrap: Игрок создан: Player в позиции (40.5, 67.5, 40.5)
GameBootstrap: Игрок готов к игре!

GameBootstrap: Инициализация камеры...
GameBootstrap: CameraController найден: CameraController
GameBootstrap: CameraPivot создан для игрока
GameBootstrap: Камера инициализирована успешно

GameBootstrap: Инициализация спавнеров врагов...
GameBootstrap: Найдено 2 спавнеров в сцене
EnemySpawner [EnemySpawner1]: Установлена цель для врагов: Player
EnemySpawner [EnemySpawner2]: Установлена цель для врагов: Player
GameBootstrap: Инициализировано 2 спавнеров врагов

=== GameBootstrap: Инициализация завершена успешно ===


================================================================================
ПУБЛИЧНЫЕ МЕТОДЫ
================================================================================

GetPlayer()
-----------
Возвращает GameObject игрока после спавна.

Пример:
  var player = GameBootstrap.GetPlayer();
  if (player != null)
      Debug.Log("Игрок в позиции: " + player.transform.position);


IsInitialized()
---------------
Проверяет, завершена ли инициализация.

Пример:
  if (bootstrap.IsInitialized())
      Debug.Log("Игра готова!");


RespawnPlayer()
---------------
Переспавнить игрока в новой безопасной позиции.

Пример:
  bootstrap.RespawnPlayer();


DeletePlayerPositionSave()
--------------------------
Удалить сохранение позиции игрока.

Пример:
  bootstrap.DeletePlayerPositionSave();
  
Context Menu:
  GameBootstrap → "Delete Player Position Save"


================================================================================
ИНТЕГРАЦИЯ С СУЩЕСТВУЮЩИМ КОДОМ
================================================================================

УДАЛИТЬ ИЗ PlayerController:
----------------------------
Если PlayerController сам инициализирует AutoSpawnService в Awake(),
это будет конфликтовать. Рекомендуется:

1. Закомментировать/удалить из PlayerController.Awake():
   // InitializeAutoSpawnService();

2. AutoSpawnService должен инициализироваться ПОСЛЕ спавна игрока


АЛЬТЕРНАТИВА - ВЫЗОВ ИЗ GameBootstrap:
--------------------------------------
Добавить в SpawnPlayer() после создания игрока:

   var playerController = spawnedPlayer.GetComponent<PlayerController>();
   if (playerController != null)
   {
       // Инициализируем AutoSpawnService
       new AutoSpawnService();
       AutoSpawnService.Instance?.Initialize(playerController);
   }


================================================================================
ОТЛАДКА
================================================================================

ПРОБЛЕМА: Игрок не спавнится
-----------------------------
Проверить:
  ✓ Префаб игрока указан в Inspector
  ✓ VoxelWorld существует в сцене
  ✓ В консоли нет ошибок
  ✓ GameBootstrap имеет execution order = -100


ПРОБЛЕМА: Игрок спавнится в воздухе
------------------------------------
  ✓ Увеличить maxWaitTime (может мир еще генерируется)
  ✓ Проверить VoxelWorld.IsWorldReady = true в консоли
  ✓ Проверить GetSafeSpawnPosition() находит блоки


ПРОБЛЕМА: Долгая загрузка
--------------------------
  ✓ Это нормально при первом запуске (генерация мира)
  ✓ Уменьшить размер мира (chunksX, chunksZ в VoxelWorld)
  ✓ Увеличить maxFramesPerChunk для быстрой генерации
  ✓ Отключить useProgressiveGeneration для мгновенной генерации


ПРИНУДИТЕЛЬНЫЙ СПАВН В КОНКРЕТНОЙ ПОЗИЦИИ:
------------------------------------------
Изменить в GameBootstrap.SpawnPlayer():

   // Вместо:
   Vector3 spawnPosition = voxelWorld.GetSafeSpawnPosition();
   
   // Использовать:
   Vector3 spawnPosition = new Vector3(40, 70, 40);


================================================================================
ПРИМЕР СЦЕНЫ
================================================================================

Hierarchy:
----------
Scene Root
├─ GameBootstrap             [GameBootstrap]
├─ VoxelWorld                [VoxelWorld]
├─ CameraController          [CameraController, CinemachineVirtualCamera]
├─ Main Camera               [Camera, CinemachineBrain]
├─ Directional Light         [Light]
├─ EnemySpawner1             [EnemySpawner]  ← NEW!
├─ EnemySpawner2             [EnemySpawner]  ← NEW!
├─ Canvas                    [Canvas, ...]
│  └─ Camera Joystick        [FloatingJoystick]
└─ EventSystem               [EventSystem, ...]

Примечание:
- Игрок НЕ должен быть в сцене
- Игрок спавнится из префаба автоматически
- CameraController должен быть в сцене
- EnemySpawner'ы (опционально) в сцене
- GameBootstrap автоматически:
  ✓ Свяжет камеру с игроком
  ✓ Найдет все спавнеры и передаст им ссылку на игрока


================================================================================
РАСШИРЕННАЯ НАСТРОЙКА
================================================================================

ДОБАВИТЬ ЛОГИКУ ПОСЛЕ ИНИЦИАЛИЗАЦИИ:
------------------------------------
Изменить InitializeGame():

   yield return StartCoroutine(SpawnPlayer());
   
   // ===== ВАША ЛОГИКА =====
   InitializeGameSystems();
   ShowMainMenu();
   StartBackgroundMusic();
   // =======================
   
   isInitialized = true;


КАСТОМНАЯ ПОЗИЦИЯ СПАВНА:
--------------------------
Создать свой метод в VoxelWorld:

   public Vector3 GetCustomSpawnPosition(int playerIndex)
   {
       // Разные позиции для разных игроков
       return new Vector3(20 + playerIndex * 10, 70, 40);
   }


================================================================================

