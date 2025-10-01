using UnityEngine;
using System.Collections;

/// <summary>
/// Снаряд, который создает ландшафт вместо разрушения
/// </summary>
public class TerrainBuilderProjectile : Projectile
{
    [Header("Terrain Building Settings")]
    [Tooltip("Тип блока для создания (0=Трава, 1=Земля, 2=Камень, 6=Уголь, 7=Золото)")]
    public int blockType = 2; // По умолчанию камень
    
    [Tooltip("Плотность заполнения (0-1). 1 = полная сфера, меньше = более разреженная")]
    [Range(0f, 1f)]
    public float fillDensity = 0.8f;
    
    [Tooltip("Создавать только на поверхности (не заполняет пустоты внутри существующих блоков)")]
    public bool surfaceOnly = false;
    
    [Tooltip("Минимальная высота для создания блоков")]
    public int minHeight = 1;
    
    [Tooltip("Максимальная высота для создания блоков")]
    public int maxHeight = 128;
    
    [Tooltip("Использовать случайное заполнение для органического вида")]
    public bool useRandomFill = true;
    
    [Tooltip("Seed для генерации случайного рисунка")]
    public int randomSeed = 0;
    
    [Header("Player Protection")]
    [Tooltip("Горизонтальный радиус защиты вокруг игрока")]
    public float playerProtectionRadius = 2f;
    
    [Tooltip("Высота защиты вокруг игрока")]
    public float playerProtectionHeight = 4f;
    
    [Tooltip("Защищать игрока от застревания в блоках")]
    public bool protectPlayer = true;

    /// <summary>
    /// Переопределяем метод урона для создания блоков вместо разрушения
    /// </summary>
    protected override void DoDamage(Vector3 hitPoint)
    {
        if (hasExploded) return; // Предотвращаем повторные "взрывы"
        
        hasExploded = true;
        
        if (VoxelWorld.Instance != null)
        {
            // Вместо разрушения - создаем ландшафт
            BuildTerrainSphere(hitPoint, radius);
        }

        // Запускаем цепную реакцию (если нужно)
        StartChainReaction();

        if (destroyOnHit) 
        {
            StartCoroutine(DestroyAfterChainReaction());
        }
    }

    /// <summary>
    /// Создает сферу из блоков
    /// </summary>
    private void BuildTerrainSphere(Vector3 center, float radius)
    {
        // Инициализируем генератор случайных чисел
        System.Random random = null;
        if (useRandomFill)
        {
            int seed = randomSeed != 0 ? randomSeed : (int)(center.x * 73856093 + center.y * 19349663 + center.z * 83492791);
            random = new System.Random(seed);
        }

        // Вычисляем границы сферы
        int radiusInt = Mathf.CeilToInt(radius);
        Vector3Int centerBlock = new Vector3Int(
            Mathf.RoundToInt(center.x),
            Mathf.RoundToInt(center.y),
            Mathf.RoundToInt(center.z)
        );

        // Проходим по всем блокам в кубе вокруг центра
        for (int x = -radiusInt; x <= radiusInt; x++)
        {
            for (int y = -radiusInt; y <= radiusInt; y++)
            {
                for (int z = -radiusInt; z <= radiusInt; z++)
                {
                    Vector3Int blockPos = centerBlock + new Vector3Int(x, y, z);
                    
                    // Проверка высоты
                    if (blockPos.y < minHeight || blockPos.y > maxHeight)
                        continue;

                    // Проверка расстояния от центра (сферическая форма)
                    float distance = Vector3.Distance(center, new Vector3(blockPos.x, blockPos.y, blockPos.z));
                    
                    if (distance <= radius)
                    {
                        // Проверяем защиту игрока
                        if (protectPlayer && IsInPlayerProtectionZone(blockPos))
                            continue;
                        
                        // Применяем плотность заполнения
                        float normalizedDistance = distance / radius;
                        float fillProbability = fillDensity * (1f - normalizedDistance * 0.5f); // Меньше блоков на краях
                        
                        if (useRandomFill && random != null)
                        {
                            if (random.NextDouble() > fillProbability)
                                continue;
                        }

                        // Режим "только поверхность"
                        if (surfaceOnly)
                        {
                            // Проверяем, есть ли рядом пустое пространство
                            if (!HasAdjacentAir(blockPos))
                                continue;
                        }

                        // Пытаемся установить блок
                        VoxelWorld.Instance.SetBlock(blockPos.x, blockPos.y, blockPos.z, blockType, false);
                    }
                }
            }
        }

        // Перестраиваем затронутые чанки после создания всех блоков
        RebuildAffectedChunks(centerBlock, radiusInt);
    }

    /// <summary>
    /// Проверяет, находится ли блок в защитной зоне вокруг игрока
    /// </summary>
    private bool IsInPlayerProtectionZone(Vector3Int blockPos)
    {
        // Находим игрока
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null)
            return false; // Если игрока нет, не защищаем
        
        Vector3 playerPos = player.transform.position;
        Vector3 blockWorldPos = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
        
        // Проверяем горизонтальное расстояние (XZ плоскость)
        float horizontalDistance = Vector2.Distance(
            new Vector2(playerPos.x, playerPos.z), 
            new Vector2(blockWorldPos.x, blockWorldPos.z)
        );
        
        if (horizontalDistance > playerProtectionRadius)
            return false; // Слишком далеко по горизонтали
        
        // Проверяем вертикальное расстояние (Y)
        float verticalDistance = Mathf.Abs(blockWorldPos.y - playerPos.y);
        
        if (verticalDistance > playerProtectionHeight)
            return false; // Слишком далеко по вертикали
        
        // Блок находится в защитной зоне
        return true;
    }
    
    /// <summary>
    /// Проверяет, есть ли рядом с блоком воздух (для режима surfaceOnly)
    /// </summary>
    private bool HasAdjacentAir(Vector3Int blockPos)
    {
        // Проверяем 6 соседних позиций
        Vector3Int[] offsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        foreach (var offset in offsets)
        {
            Vector3Int checkPos = blockPos + offset;
            
            // Используем Physics.CheckBox для проверки наличия коллайдера
            // Если коллайдера нет - значит там воздух
            Collider[] colliders = Physics.OverlapBox(
                new Vector3(checkPos.x, checkPos.y, checkPos.z), 
                Vector3.one * 0.4f
            );
            
            if (colliders.Length == 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Перестраивает все чанки, затронутые созданием блоков
    /// </summary>
    private void RebuildAffectedChunks(Vector3Int center, int radius)
    {
        // Вычисляем диапазон затронутых чанков
        int minChunkX = (center.x - radius) / VoxelChunk16.WIDTH;
        int maxChunkX = (center.x + radius) / VoxelChunk16.WIDTH;
        int minChunkZ = (center.z - radius) / VoxelChunk16.DEPTH;
        int maxChunkZ = (center.z + radius) / VoxelChunk16.DEPTH;

        // Перестраиваем каждый затронутый чанк один раз
        for (int cx = minChunkX; cx <= maxChunkX; cx++)
        {
            for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
            {
                VoxelWorld.Instance.RebuildChunk(cx, cz);
            }
        }
    }

    /// <summary>
    /// Визуализация в редакторе
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // Визуализация радиуса создания блоков (зеленый для создания, в отличие от красного для разрушения)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
        
        // Визуализация радиуса цепной реакции
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chainReactionRadius);
        
        // Визуализация зоны защиты игрока
        if (protectPlayer)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                Vector3 playerPos = player.transform.position;
                
                // Рисуем цилиндр защиты (синий)
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
                
                // Верхний круг
                DrawCircle(playerPos + Vector3.up * playerProtectionHeight, playerProtectionRadius, Vector3.up);
                
                // Нижний круг
                DrawCircle(playerPos - Vector3.up * playerProtectionHeight, playerProtectionRadius, Vector3.up);
                
                // Вертикальные линии
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 2f / 8f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * playerProtectionRadius;
                    Gizmos.DrawLine(
                        playerPos + offset + Vector3.up * playerProtectionHeight,
                        playerPos + offset - Vector3.up * playerProtectionHeight
                    );
                }
            }
        }
    }
    
    /// <summary>
    /// Вспомогательный метод для рисования круга в Gizmos
    /// </summary>
    private void DrawCircle(Vector3 center, float radius, Vector3 normal, int segments = 32)
    {
        Vector3 forward = Vector3.Slerp(normal, -normal, 0.5f);
        if (forward == Vector3.zero) forward = Vector3.up;
        
        Vector3 right = Vector3.Cross(normal, forward).normalized * radius;
        Vector3 up = Vector3.Cross(right, normal).normalized * radius;
        
        Vector3 prevPoint = center + right;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 nextPoint = center + right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}

