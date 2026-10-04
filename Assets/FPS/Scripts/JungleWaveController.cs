using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.Jungle
{
    public class JungleWaveController : MonoBehaviour
    {
        [Header("Required References")]
        public GameObject EnemyPrefab;
        [Tooltip("Spawn point directly outside the LEFT window")]
        public Transform Window1SpawnPoint;
        [Tooltip("Spawn point directly outside the RIGHT window")]
        public Transform Window2SpawnPoint;

        [Header("Wave Settings")]
        [Tooltip("Total enemies to spawn in each wave")]
        public int BotsPerWave = 6;
        [Tooltip("Total number of waves")]
        public int TotalWaves = 3;
        [Tooltip("Time delay before the next wave starts after clearing the current one")]
        public float DelayBetweenWaves = 3f;

        [Header("Dynamic Spawn Logic Settings")]
        [Tooltip("Maximum enemies alive on the field at the exact same time")]
        public int MaxAlive = 4;
        [Tooltip("Time between individual enemy spawns")]
        public float SpawnInterval = 2f;
        [Tooltip("Maximum enemies that can be simultaneously assigned to a single window")]
        public int MaxPerWindow = 2;
        
        [Header("Bot Elevation Fix")]
        [Tooltip("Adds extra hover height to the bot so it can clearly shoot OVER the window ledge.")]
        public float ExtraHoverHeight = 1.0f;

        // Runtime State
        private int m_CurrentWave = 1;
        private int m_BotsSpawnedThisWave = 0;
        private int m_BotsKilledThisWave = 0;
        private bool m_WaitingForNextWave = false;
        private float m_NextWaveTime = 0f;
        private float m_NextSpawnTime = 0f;
        
        // Anti-repetition tracking
        private int m_LastUsedWindow = 0; // 1 or 2
        private int m_SameWindowCount = 0;

        // Track alive bots per window to enforce MaxPerWindow
        private List<GameObject> m_Window1Bots = new List<GameObject>();
        private List<GameObject> m_Window2Bots = new List<GameObject>();
        
        private Unity.FPS.Gameplay.PlayerCharacterController m_Player;

        void Awake()
        {
            EventManager.AddListener<EnemyKillEvent>(OnEnemyKilled);
        }

        void Start()
        {
            m_Player = FindAnyObjectByType<Unity.FPS.Gameplay.PlayerCharacterController>();
            
            // Validate references
            if (EnemyPrefab == null || Window1SpawnPoint == null || Window2SpawnPoint == null)
            {
                Debug.LogError("[JungleWaveController] Missing references in Inspector! Please assign EnemyPrefab, Window1SpawnPoint, and Window2SpawnPoint.");
                enabled = false;
                return;
            }

            StartWave(1);
        }

        void Update()
        {
            if (m_CurrentWave > TotalWaves) return;

            if (m_WaitingForNextWave)
            {
                if (Time.time >= m_NextWaveTime)
                {
                    StartWave(m_CurrentWave + 1);
                }
                return;
            }

            // Clean up lists (remove bots that were killed/destroyed)
            m_Window1Bots.RemoveAll(b => b == null || !b.activeInHierarchy);
            m_Window2Bots.RemoveAll(b => b == null || !b.activeInHierarchy);

            int totalAlive = m_Window1Bots.Count + m_Window2Bots.Count;

            // Check if we meet the 3 conditions to spawn:
            // 1. Wave has enemies left to spawn
            // 2. Room on the field (alive < maxAlive)
            // 3. Enough time has passed since last spawn
            if (m_BotsSpawnedThisWave < BotsPerWave && totalAlive < MaxAlive && Time.time >= m_NextSpawnTime)
            {
                TrySpawnBot();
            }
        }
        
        void TrySpawnBot()
        {
            bool window1Full = m_Window1Bots.Count >= MaxPerWindow;
            bool window2Full = m_Window2Bots.Count >= MaxPerWindow;

            // 4. If both windows are full, wait. Nothing spawns.
            if (window1Full && window2Full)
            {
                return;
            }

            // 1. Pick a window at random
            int chosenWindow = Random.Range(1, 3); // Returns 1 or 2

            // 3. If same window was used twice in a row, switch to the other
            if (m_LastUsedWindow == chosenWindow && m_SameWindowCount >= 2)
            {
                chosenWindow = chosenWindow == 1 ? 2 : 1;
            }

            // 2. If the chosen window is full, switch to the other window
            if (chosenWindow == 1 && window1Full) chosenWindow = 2;
            else if (chosenWindow == 2 && window2Full) chosenWindow = 1;

            // Update anti-repetition trackers
            if (chosenWindow == m_LastUsedWindow)
            {
                m_SameWindowCount++;
            }
            else
            {
                m_LastUsedWindow = chosenWindow;
                m_SameWindowCount = 1;
            }

            // Spawn at the chosen window
            Transform spawnPoint = chosenWindow == 1 ? Window1SpawnPoint : Window2SpawnPoint;
            SpawnBotAt(spawnPoint, chosenWindow);

            m_BotsSpawnedThisWave++;
            m_NextSpawnTime = Time.time + SpawnInterval;
        }

        void SpawnBotAt(Transform spawnPoint, int windowIndex)
        {
            // Add a very small random offset so they don't spawn EXACTLY inside each other physically
            Vector3 offset = Random.insideUnitSphere * 0.5f;
            offset.y = 0f;
            Vector3 pos = spawnPoint.position + offset;

            var bot = Instantiate(EnemyPrefab, pos, spawnPoint.rotation);
            bot.name = $"Wave{m_CurrentWave}_Bot_{m_BotsSpawnedThisWave + 1}_Win{windowIndex}";

            if (windowIndex == 1) m_Window1Bots.Add(bot);
            else m_Window2Bots.Add(bot);

            // Force aggressive detection overrides
            var detection = bot.GetComponentInChildren<Unity.FPS.AI.DetectionModule>();
            if (detection != null)
            {
                detection.DetectionRange = 100f;
                detection.AttackRange = 50f;
                if (m_Player != null) detection.OnDamaged(m_Player.gameObject);
            }

            // Fix Elevation: Boost the NavMeshAgent baseOffset so they float higher and shoot cleanly over the ledge!
            var agent = bot.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.baseOffset += ExtraHoverHeight;
            }
        }

        void StartWave(int wave)
        {
            m_WaitingForNextWave = false;
            if (wave > TotalWaves)
            {
                Debug.Log("[JungleWaveController] All waves completed!");
                return;
            }

            m_CurrentWave = wave;
            m_BotsSpawnedThisWave = 0;
            m_BotsKilledThisWave = 0;
            m_NextSpawnTime = Time.time; // Spawn the first bot immediately
            
            Debug.Log($"[JungleWaveController] ▶ WAVE {m_CurrentWave} STARTED!");
        }

        void OnEnemyKilled(EnemyKillEvent evt)
        {
            if (m_CurrentWave > TotalWaves || m_WaitingForNextWave) return;

            m_BotsKilledThisWave++;
            Debug.Log($"[JungleWaveController] Kill {m_BotsKilledThisWave}/{BotsPerWave} in wave {m_CurrentWave}.");

            if (m_BotsKilledThisWave >= BotsPerWave)
            {
                if (m_CurrentWave < TotalWaves)
                {
                    Debug.Log($"[JungleWaveController] Wave {m_CurrentWave} cleared! Next wave in {DelayBetweenWaves}s.");
                    m_WaitingForNextWave = true;
                    m_NextWaveTime = Time.time + DelayBetweenWaves;
                }
                else
                {
                    Debug.Log("[JungleWaveController] Final wave cleared!");
                }
            }
        }
    }
}
