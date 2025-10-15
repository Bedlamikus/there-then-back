====================================================
   СИСТЕМА ПУЛОВ ПАРТИКЛОВ ВЗРЫВОВ
====================================================

ОПИСАНИЕ:
---------
Оптимизированная система для проигрывания эффектов взрывов.
Используется для взрывов снарядов И вспышек выстрелов (через один эффект).
Использует пулы для переиспользования партиклов вместо создания/уничтожения.

КОМПОНЕНТЫ:
-----------
1. ProjectileType.cs - Enum с типами снарядов (Pistol, Rocket, Dinamit, Rock, Custom)
2. ExplosionParticlePool.cs - Пул для одного типа эффекта (взрыв или вспышка)
3. ExplosionParticleManager.cs - Менеджер всех пулов (взрывы + вспышки)
4. ParticleAutoHide.cs - Компонент для автоскрытия партиклов

НАСТРОЙКА:
----------
1. Создайте пустой GameObject в сцене
2. Добавьте компонент ExplosionParticleManager
3. В инспекторе добавьте пулы для каждого типа снаряда:

   EFFECT POOLS:
      - Pistol → PistolExplosion (Pool: 5-15)
      - Rocket → RocketExplosion (Pool: 5-20)
      - Dinamit → DinamitExplosion (Pool: 5-25)
      - Rock → RockExplosion (Pool: 5-15)
      - MuzzleFlash → MuzzleFlashEffect (Pool: 3-8)
   
   ПРИМЕЧАНИЕ: Разные эффекты для разных целей:
      - MuzzleFlash → вспышка на стволе (одинаковая для всех оружий)
      - Pistol/Rocket/Dinamit/Rock → взрывы при попадании (разные для каждого типа)

4. Настройте префабы снарядов:
   - Откройте префаб снаряда
   - В компоненте Projectile установите поле "Projectile Type"
   - Pistol для пистолета
   - Rocket для ракеты
   - Dinamit для динамита
   - Rock для камня

СОЗДАНИЕ ПАРТИКЛОВ:
-------------------
1. Создайте префаб партикла взрыва в Unity
2. Добавьте ParticleSystem компонент
3. Настройте визуальные эффекты
4. (Опционально) Добавьте ParticleAutoHide для автоскрытия

РАБОТА СИСТЕМЫ:
---------------
ВЗРЫВЫ СНАРЯДОВ:
1. Снаряд взрывается → вызывает GlobalEvents.ProjectileExploded(hitPoint, type)
2. ExplosionParticleManager получает событие
3. Выбирает соответствующий пул по типу снаряда
4. Пул достает неактивный партикл (или создает новый)
5. Партикл позиционируется и проигрывается
6. ParticleAutoHide автоматически скрывает партикл после проигрывания
7. Партикл возвращается в пул для переиспользования

ВСПЫШКИ ВЫСТРЕЛОВ:
1. Оружие стреляет (игрок/бот) → вызывает GlobalEvents.ProjectileExploded(shootPoint, MuzzleFlash)
2. ExplosionParticleManager получает событие с типом MuzzleFlash
3. Выбирает пул MuzzleFlash (один партикл для всех оружий)
4. Партикл позиционируется в точке выстрела и проигрывается
5. ParticleAutoHide автоматически скрывает партикл после проигрывания
6. Партикл возвращается в пул для переиспользования

РАЗДЕЛЕНИЕ: MuzzleFlash для вспышек + разные эффекты для взрывов!

СОБЫТИЙНАЯ АРХИТЕКТУРА:
------------------------
- ExplosionParticleManager - обычный MonoBehaviour
- НЕТ Singleton паттерна - не нужен!
- Работает ТОЛЬКО через событийную систему (GlobalEvents)
- Можно иметь несколько менеджеров в разных сценах
- Настройка "Persist Between Scenes" для DontDestroyOnLoad (по умолчанию OFF)

ОПТИМИЗАЦИЯ:
------------
- Партиклы НЕ уничтожаются, а скрываются и переиспользуются
- Начальный размер пула: 5 партиклов (настраиваемо)
- Максимальный размер пула: 20 партиклов (настраиваемо)
- Если пул переполнен, переиспользуется первый партикл

ИСПОЛЬЗОВАНИЕ:
--------------
Система работает автоматически через ОДНО событие!

ВЗРЫВЫ (автоматически в Projectile.cs):
   GlobalEvents.ProjectileExploded.Invoke(hitPoint, projectileType);

ВСПЫШКИ (автоматически в Weapon.cs / PlayerWeaponController.cs / EnemyWeaponController.cs):
   GlobalEvents.ProjectileExploded.Invoke(shootPoint.position, muzzleFlashType);

ExplosionParticleManager автоматически:
   - Получает событие с типом эффекта
   - Выбирает нужный пул по типу (MuzzleFlash или тип снаряда)
   - Проигрывает партикл в указанной позиции
   - Скрывает обратно в пул

РАЗДЕЛЕНИЕ: MuzzleFlash для вспышек + разные взрывы! НЕТ Singleton! 🎉

ПРИМЕЧАНИЯ:
-----------
- Один ExplosionParticleManager на сцену (рекомендуется)
- Партиклы автоматически скрываются после проигрывания
- Пулы создаются при старте сцены
- Опционально: DontDestroyOnLoad через настройку "Persist Between Scenes"
- Партиклы в пуле скрыты и не потребляют ресурсы
- Работает через событийную систему - Singleton не обязателен!

ПРИМЕР НАСТРОЙКИ:
-----------------
ExplosionParticleManager:
  
  Effect Pools (5):
    [0] Pistol
        - Particle Prefab: PistolExplosion
        - Initial Pool Size: 5
        - Max Pool Size: 15
    [1] Rocket
        - Particle Prefab: RocketExplosion
        - Initial Pool Size: 5
        - Max Pool Size: 20
    [2] Dinamit
        - Particle Prefab: DinamitExplosion
        - Initial Pool Size: 5
        - Max Pool Size: 25
    [3] Rock
        - Particle Prefab: RockExplosion
        - Initial Pool Size: 5
        - Max Pool Size: 15
    [4] MuzzleFlash
        - Particle Prefab: MuzzleFlashEffect
        - Initial Pool Size: 3
        - Max Pool Size: 8

РЕКОМЕНДАЦИИ:
-------------
- MuzzleFlash: короткая вспышка (0.1-0.3 сек), маленький пул (3-8)
- Взрывы: разные эффекты для каждого типа снаряда (0.5-2 сек)
- Партикл вспышки должен хорошо выглядеть в точке ствола
- Партиклы взрывов должны хорошо выглядеть в месте попадания

====================================================

