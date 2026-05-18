using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wcft.Core
{
    /// <summary>
    /// Runtime QA/dev panel. Available in the editor and development builds.
    /// Toggle with F1. Run a smoke QA pass with F2.
    /// </summary>
    public sealed class DevTools : MonoBehaviour
    {
        const int MaxLogLines = 14;

        static DevTools instance;

        readonly List<string> qaLog = new List<string>();
        Rect windowRect = new Rect(24f, 24f, 520f, 680f);
        Vector2 scroll;
        bool visible;
        int selectedLevel = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
                return;

            if (instance != null)
                return;

            var go = new GameObject("DevTools");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DevTools>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                visible = !visible;

            if (Input.GetKeyDown(KeyCode.F2))
            {
                visible = true;
                RunSmokeQA();
            }
        }

        void OnGUI()
        {
            if (!visible)
                return;

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "WE CAN FIX THIS - DEVTOOLS");
        }

        void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll);

            DrawHeader();
            DrawLevelTools();
            DrawGameplayTools();
            DrawQaTools();
            DrawLog();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        void DrawHeader()
        {
            LevelDefinition level = LevelProgression.Current;
            GUILayout.Label($"Scene: {SceneManager.GetActiveScene().name}");
            GUILayout.Label($"Level: {level.Index}/{LevelProgression.LevelCount} - {level.Name} | Duration: {level.DurationSeconds:0}s");
            GUILayout.Label($"Layout: ready={ShipLayoutGenerator.IsReady} seed={ShipLayoutGenerator.CurrentSeed} areas={ShipLayoutGenerator.RoomCenters?.Count ?? 0}");
            GUILayout.Label($"TimeScale: {Time.timeScale:0.00} | Players: {GetPlayerCount()} | Stations: {RepairStation.ActiveStations.Count}");

            global::FailureSystem failureSystem = global::FailureSystem.Instance;
            if (failureSystem != null)
                GUILayout.Label($"Failures: total={failureSystem.TotalFailures} interval={failureSystem.CurrentInterval:0.0}s max={failureSystem.MaxSimultaneousBroken}");

            GUILayout.Space(8f);
        }

        void DrawLevelTools()
        {
            GUILayout.Label("Level Progression");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Level 1")) selectedLevel = 1;
            if (GUILayout.Button("Level 2")) selectedLevel = 2;
            if (GUILayout.Button("Level 3")) selectedLevel = 3;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Set L{selectedLevel} + Reload Gameplay"))
            {
                LevelProgression.SetCurrentLevelForTesting(selectedLevel);
                Time.timeScale = 1f;
                SceneLoader.LoadScene(GameConfig.SCENE_GAMEPLAY);
                AddLog($"Loaded gameplay at level {selectedLevel}.");
            }

            if (GUILayout.Button("Timer Expire"))
            {
                global::GameManager.Instance?.OnTimerExpired();
                AddLog("Forced timer expiration.");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Advance Level"))
            {
                bool advanced = LevelProgression.AdvanceOrComplete();
                AddLog(advanced ? $"Advanced to {LevelProgression.Current.Name}." : "Progression complete; current level is final.");
            }

            if (GUILayout.Button("Reset Progression"))
            {
                LevelProgression.Reset();
                AddLog("Progression reset to level 1.");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload Active"))
            {
                Time.timeScale = 1f;
                SceneLoader.ReloadActiveScene();
            }

            if (GUILayout.Button("Load Gameplay"))
            {
                Time.timeScale = 1f;
                SceneLoader.LoadScene(GameConfig.SCENE_GAMEPLAY);
            }

            if (GUILayout.Button("Load Lobby"))
            {
                Time.timeScale = 1f;
                LevelProgression.Reset();
                SceneLoader.LoadScene(GameConfig.SCENE_LOBBY);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
        }

        void DrawGameplayTools()
        {
            GUILayout.Label("Gameplay Systems");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Random Failure")) TriggerRandomFailure();
            if (GUILayout.Button("Break All")) BreakAllStations();
            if (GUILayout.Button("Repair All")) RepairAllStations();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ship -25 HP"))
            {
                global::ShipHealth.Instance?.ApplyDamage(25f);
                AddLog("Applied 25 ship damage.");
            }

            if (GUILayout.Button("Ship +25 HP"))
            {
                global::ShipHealth.Instance?.Repair(25f);
                AddLog("Repaired 25 ship health.");
            }

            if (GUILayout.Button(Time.timeScale == 0f ? "Resume Time" : "Pause Time"))
            {
                Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
                AddLog($"Time scale set to {Time.timeScale:0.00}.");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop FailureSystem"))
            {
                global::FailureSystem.Instance?.SetActive(false);
                AddLog("FailureSystem stopped.");
            }

            if (GUILayout.Button("Start FailureSystem"))
            {
                global::FailureSystem.Instance?.SetActive(true);
                AddLog("FailureSystem started.");
            }

            if (GUILayout.Button("Core-X Stop"))
            {
                global::CoreXBrain.Instance?.StopDirector();
                AddLog("Core-X director stopped.");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
        }

        void DrawQaTools()
        {
            GUILayout.Label("QA");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Smoke QA")) RunSmokeQA();
            if (GUILayout.Button("Copy Status To Log")) AddLog(BuildStatusReport());
            if (GUILayout.Button("Clear Log")) qaLog.Clear();
            GUILayout.EndHorizontal();

            GUILayout.Label("Shortcuts: F1 toggle panel, F2 run smoke QA.");
            GUILayout.Space(8f);
        }

        void DrawLog()
        {
            GUILayout.Label("QA Log");
            if (qaLog.Count == 0)
            {
                GUILayout.Label("No QA runs yet.");
                return;
            }

            foreach (string line in qaLog)
                GUILayout.Label(line);
        }

        void TriggerRandomFailure()
        {
            global::RepairStation[] candidates = RepairStation.ActiveStations
                .Where(station => station != null
                    && (station.State == RepairStation.StationState.Functional
                        || station.State == RepairStation.StationState.Fixed))
                .ToArray();

            if (candidates.Length == 0)
            {
                AddLog("No functional station available for random failure.");
                return;
            }

            global::RepairStation station = candidates[Random.Range(0, candidates.Length)];
            if (global::FailureSystem.Instance != null)
                global::FailureSystem.Instance.ForceFailStation(station);
            else
                station.BreakStation();

            AddLog($"Forced random failure: {station.Type}.");
        }

        void BreakAllStations()
        {
            int count = 0;
            foreach (global::RepairStation station in RepairStation.ActiveStations)
            {
                if (station == null)
                    continue;

                station.BreakStation();
                count++;
            }

            AddLog($"Broke {count} stations.");
        }

        void RepairAllStations()
        {
            int count = 0;
            foreach (global::RepairStation station in RepairStation.ActiveStations)
            {
                if (station == null)
                    continue;

                station.ApplyRemoteRepairBoost(1f);
                count++;
            }

            AddLog($"Applied repair boost to {count} stations.");
        }

        void RunSmokeQA()
        {
            var results = new List<(string label, bool pass)>
            {
                ("GameManager exists", global::GameManager.Instance != null),
                ("PlayerManager exists", global::PlayerManager.Instance != null),
                ("LevelProgression valid", LevelProgression.Current != null && LevelProgression.Current.DurationSeconds > 0f),
                ("Gameplay scene name configured", !string.IsNullOrWhiteSpace(GameConfig.SCENE_GAMEPLAY)),
                ("No paused time leak", Time.timeScale > 0f),
            };

            if (SceneManager.GetActiveScene().name == GameConfig.SCENE_GAMEPLAY)
            {
                results.Add(("Ship layout ready", ShipLayoutGenerator.IsReady));
                results.Add(("Room centers published", ShipLayoutGenerator.RoomCenters != null && ShipLayoutGenerator.RoomCenters.Count >= 8));
                results.Add(("Player spawn positions published", ShipLayoutGenerator.PlayerSpawnPositions != null && ShipLayoutGenerator.PlayerSpawnPositions.Count > 0));
                results.Add(("Repair stations registered", RepairStation.ActiveStations.Count >= 4));
                results.Add(("ShipHealth exists", global::ShipHealth.Instance != null));
                results.Add(("FailureSystem exists", global::FailureSystem.Instance != null));
                results.Add(("CoreXBrain exists", global::CoreXBrain.Instance != null));
            }

            int passed = results.Count(result => result.pass);
            AddLog($"Smoke QA {passed}/{results.Count} passed");
            foreach ((string label, bool pass) in results)
                AddLog($"{(pass ? "PASS" : "FAIL")} - {label}");
        }

        string BuildStatusReport()
        {
            var sb = new StringBuilder();
            LevelDefinition level = LevelProgression.Current;
            sb.Append($"Scene={SceneManager.GetActiveScene().name}; ");
            sb.Append($"Level={level.Index}; ");
            sb.Append($"LayoutReady={ShipLayoutGenerator.IsReady}; ");
            sb.Append($"Rooms={ShipLayoutGenerator.RoomCenters?.Count ?? 0}; ");
            sb.Append($"Stations={RepairStation.ActiveStations.Count}; ");
            sb.Append($"Players={GetPlayerCount()}; ");
            sb.Append($"ShipHP={(global::ShipHealth.Instance != null ? global::ShipHealth.Instance.HealthPercent : 0f):0.00}; ");
            sb.Append($"Failures={(global::FailureSystem.Instance != null ? global::FailureSystem.Instance.TotalFailures : 0)}");
            return sb.ToString();
        }

        int GetPlayerCount()
        {
            return global::PlayerManager.Instance != null ? global::PlayerManager.Instance.PlayerCount : 0;
        }

        void AddLog(string message)
        {
            qaLog.Insert(0, message);
            while (qaLog.Count > MaxLogLines)
                qaLog.RemoveAt(qaLog.Count - 1);

            Debug.Log($"[DevTools] {message}");
        }
    }
}
