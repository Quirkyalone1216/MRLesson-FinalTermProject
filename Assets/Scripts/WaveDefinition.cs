using UnityEngine;

[CreateAssetMenu(menuName="Game/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    public int enemiesToSpawn = 10;
    public float spawnInterval = 0.8f;
    public int maxConcurrent = 8;

    public float preWaveDelay = 3f;   // 波開始前倒數
    public float postWaveDelay = 2f;  // 波結束後緩衝

    public GameObject[] enemyPrefabs;

    public GameObject PickPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return null;
        return enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
    }
}
