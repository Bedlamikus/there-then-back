using UnityEngine;

/// <summary>
/// Конфигурация параметров дерева для процедурной генерации
/// </summary>
[CreateAssetMenu(fileName = "TreeConfig", menuName = "Voxel/Tree Config")]
public class TreeConfig : ScriptableObject
{
    [Header("Tree Size")]
    [Tooltip("Минимальная высота ствола (блоки)")]
    [Range(4, 30)]
    public int minTrunkHeight = 5;
    
    [Tooltip("Максимальная высота ствола (блоки)")]
    [Range(4, 30)]
    public int maxTrunkHeight = 15;
    
    [Tooltip("Радиус ствола (блоки) - всегда 1 блок в ширину")]
    public int trunkRadius = 1; // Используется для проверок размера, ствол всегда 1 блок
    
    [Header("Branches")]
    [Tooltip("Минимальное количество веток")]
    [Range(0, 12)]
    public int minBranches = 3;
    
    [Tooltip("Максимальное количество веток")]
    [Range(0, 12)]
    public int maxBranches = 6;
    
    [Tooltip("Минимальная длина ветки (блоки)")]
    [Range(1, 8)]
    public int minBranchLength = 2;
    
    [Tooltip("Максимальная длина ветки (блоки)")]
    [Range(1, 8)]
    public int maxBranchLength = 4;
    
    [Tooltip("Высота начала веток (0 = у основания, 1 = на вершине)")]
    [Range(0f, 1f)]
    public float branchStartHeight = 0.5f;
    
    [Tooltip("Угол отклонения веток от вертикали (градусы)")]
    [Range(0f, 60f)]
    public float branchAngle = 35f;
    
    [Tooltip("Вероятность ответвления у ветки")]
    [Range(0f, 1f)]
    public float subBranchProbability = 0.3f;
    
    [Header("Crown (Листва)")]
    [Tooltip("Радиус кроны (блоки)")]
    [Range(2, 8)]
    public int crownRadius = 5;
    
    [Tooltip("Высота кроны (блоки)")]
    [Range(2, 12)]
    public int crownHeight = 7;
    
    [Tooltip("Форма кроны (0 = сфера, 1 = продолговатая вверх)")]
    [Range(0f, 1f)]
    public float crownElongation = 0.3f;
    
    [Tooltip("Плотность листвы (0 = редкая, 1 = плотная)")]
    [Range(0f, 1f)]
    public float crownDensity = 0.85f;
    
    [Tooltip("Создавать листву на концах веток")]
    public bool leavesOnBranches = true;
    
    [Tooltip("Радиус листвы на ветках")]
    [Range(1, 4)]
    public int branchCrownRadius = 2;
    
    [Header("Noise Settings")]
    [Tooltip("Масштаб шума для органичности формы")]
    [Range(1f, 20f)]
    public float noiseScale = 8f;
    
    [Tooltip("Порог шума для создания блоков (меньше = меньше пропусков = пышнее)")]
    [Range(0f, 1f)]
    public float noiseThreshold = 0.15f;
    
    [Tooltip("Использовать шум для органичности")]
    public bool useNoise = true;
    
    [Header("Block Types")]
    [Tooltip("ID типа блока для ствола и веток")]
    public int woodBlockType = 3;
    
    [Tooltip("ID типа блока для листвы")]
    public int leavesBlockType = 4;
    
    [Header("Generation Rules")]
    [Tooltip("Минимальное количество свободных блоков над стволом")]
    [Range(5, 30)]
    public int minClearanceAbove = 15;
    
    [Tooltip("Проверять что под стволом есть земля/трава")]
    public bool requireSoilBelow = true;
    
    [Tooltip("Допустимые типы почвы для посадки (0=Трава, 1=Земля)")]
    public int[] validSoilTypes = new int[] { 0, 1 }; // Только Трава и Земля
}

