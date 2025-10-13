using UnityEngine;

/// <summary>
/// Конфигурация параметров каньона для процедурной генерации
/// </summary>
[CreateAssetMenu(fileName = "CanyonConfig", menuName = "Voxel/Canyon Config")]
public class CanyonConfig : ScriptableObject
{
    [Header("Canyon Size")]
    [Tooltip("Минимальная глубина каньона (блоки)")]
    [Range(5, 40)]
    public int minDepth = 15;
    
    [Tooltip("Максимальная глубина каньона (блоки)")]
    [Range(5, 40)]
    public int maxDepth = 25;
    
    [Tooltip("Ширина каньона на дне (блоки)")]
    [Range(3, 15)]
    public int bottomWidth = 5;
    
    [Tooltip("Ширина каньона сверху (блоки)")]
    [Range(5, 25)]
    public int topWidth = 12;
    
    [Header("Canyon Path")]
    [Tooltip("Минимальная длина главного каньона (блоки)")]
    [Range(20, 100)]
    public int minLength = 40;
    
    [Tooltip("Максимальная длина главного каньона (блоки)")]
    [Range(20, 100)]
    public int maxLength = 70;
    
    [Tooltip("Извилистость пути (0 = прямой, 1 = очень извилистый)")]
    [Range(0f, 1f)]
    public float pathCurvature = 0.4f;
    
    [Tooltip("Масштаб шума для пути каньона")]
    [Range(5f, 50f)]
    public float pathNoiseScale = 20f;
    
    [Header("Branches")]
    [Tooltip("Минимальное количество ответвлений")]
    [Range(0, 5)]
    public int minBranches = 1;
    
    [Tooltip("Максимальное количество ответвлений")]
    [Range(0, 5)]
    public int maxBranches = 3;
    
    [Tooltip("Минимальная длина ответвления (процент от основного)")]
    [Range(0.2f, 0.8f)]
    public float minBranchLengthRatio = 0.3f;
    
    [Tooltip("Максимальная длина ответвления (процент от основного)")]
    [Range(0.3f, 1f)]
    public float maxBranchLengthRatio = 0.6f;
    
    [Tooltip("Угол ответвления от главного каньона (градусы)")]
    [Range(30f, 90f)]
    public float branchAngle = 60f;
    
    [Header("Slope Profile")]
    [Tooltip("Угол наклона склонов (0 = вертикальный, 1 = пологий)")]
    [Range(0f, 1f)]
    public float slopeAngle = 0.5f;
    
    [Tooltip("Добавлять неровности на склонах")]
    public bool addSlopeRoughness = true;
    
    [Tooltip("Масштаб неровностей склонов")]
    [Range(1f, 10f)]
    public float slopeRoughnessScale = 4f;
    
    [Tooltip("Интенсивность неровностей")]
    [Range(0f, 1f)]
    public float slopeRoughnessIntensity = 0.3f;
    
    [Header("Terrain Modification")]
    [Tooltip("Размещать землю на склонах")]
    public bool placeDirtOnSlopes = true;
    
    [Tooltip("Толщина слоя земли на склонах (блоки)")]
    [Range(1, 3)]
    public int slopeDirtThickness = 2;
    
    [Tooltip("Размещать камни на дне каньона")]
    public bool placeRocksOnBottom = true;
    
    [Tooltip("Плотность камней на дне (0 = редко, 1 = густо)")]
    [Range(0f, 1f)]
    public float bottomRockDensity = 0.7f;
    
    [Tooltip("Высота слоя камней на дне (блоки)")]
    [Range(1, 5)]
    public int bottomRockHeight = 2;
    
    [Header("Protected Blocks")]
    [Tooltip("Типы блоков которые не изменяются (ресурсы: уголь, золото и т.д.)")]
    public int[] protectedBlockTypes = new int[] { 6, 7 }; // Уголь, Золото
    
    [Header("Generation Rules")]
    [Tooltip("Минимальная высота для генерации каньона")]
    [Range(20, 80)]
    public int minGenerationHeight = 30;
    
    [Tooltip("Не прорезать каньон ниже этой высоты")]
    [Range(10, 50)]
    public int minBottomHeight = 15;
}

