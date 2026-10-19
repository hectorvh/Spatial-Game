using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Obstacle Prefabs")]
    public GameObject[] obstaclePrefabs; 
    
    [Header("Difficulty Settings")]
    public float currentSpeed = 10.0f; 
    public float maxSpeed = 50.0f; 
    public float difficultyIncreaseRate = 0.5f; 
    
    [Header("Spawn Settings")]
    public float[] spawnLanesX = new float[] { -4.10f, -2.10f, -0.10f }; 
    private float spawnPosZ = 28.0f; 
    public float spawnPosY = 0.86f; 
    
    private float timeSinceLastSpawn = 0.0f; 
    private PlayerControllerX playerControllerScript; 
    
    void Start()
    {
        playerControllerScript = GameObject.Find("AstroLaika").GetComponent<PlayerControllerX>(); 
    }

    void Update()
    {
        if (playerControllerScript != null && playerControllerScript.isGameOver == false) 
        {
            if (currentSpeed < maxSpeed)
            {
                currentSpeed += difficultyIncreaseRate * Time.deltaTime;
            }

            float currentSpawnCooldown = 20.0f / currentSpeed;

            timeSinceLastSpawn += Time.deltaTime;
            if (timeSinceLastSpawn >= currentSpawnCooldown)
            {
                SpawnObstacle();
                timeSinceLastSpawn = 0.0f; 
            }
        }
    }
    
    void SpawnObstacle()
    {
        if (obstaclePrefabs.Length == 0) return;

        float randomX = spawnLanesX[Random.Range(0, spawnLanesX.Length)];
        int randomObstacleIndex = Random.Range(0, obstaclePrefabs.Length);
        GameObject obstacleToSpawn = obstaclePrefabs[randomObstacleIndex];
        
        Vector3 spawnLocation = new Vector3(randomX, spawnPosY, spawnPosZ);
        Instantiate(obstacleToSpawn, spawnLocation, obstacleToSpawn.transform.rotation);
    }
}