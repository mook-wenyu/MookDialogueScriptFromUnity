using System;
using MookDialogueScript;
using UnityEngine;

public class DialogueMgr : MonoBehaviour
{
    public static DialogueMgr Instance { get; private set; }

    // 创建对话管理器
    public Runner RunMgrs { get; private set; }
    
    void Awake()
    {
        Instance = this;
        Initialize();
    }
    
    public void Initialize()
    {
        Debug.Log("开始初始化对话系统");
        RunMgrs = new Runner();
        
        // 注册玩家对象
        var player = new Player("二狗");
        // 使用Runner的RegisterObject方法注册玩家对象
        RunMgrs.RegisterObject("player", player);

        // 注册一些测试变量
        RunMgrs.RegisterVariable("gold", new RuntimeValue(100));
        RunMgrs.RegisterVariable("has_key", new RuntimeValue(false));
    }

    [ContextMenu("Show Performance Statistics")]
    public void ShowPerformanceStatistics()
    {
        if (RunMgrs?.Context != null)
        {
            UnityEngine.Debug.Log("=== 对话系统性能统计 ===");
            var stats = RunMgrs.Context.GetPerformanceStatistics();
            
            foreach (var kvp in stats)
            {
                UnityEngine.Debug.Log($"{kvp.Key}: {kvp.Value}");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("对话系统未初始化");
        }
    }

    [ContextMenu("Clear All Caches")]
    public void ClearAllCaches()
    {
        if (RunMgrs?.Context != null)
        {
            RunMgrs.Context.ClearAllCaches();
            UnityEngine.Debug.Log("所有缓存已清理");
        }
        else
        {
            Helper.ClearCache();
            UnityEngine.Debug.Log("Helper缓存已清理");
        }
    }
    
    /// <summary>
    /// 游戏系统类 - 只包含一些静态变量
    /// </summary>
    public static class GameSystem
    {
        // 静态变量示例
        [ScriptVar("game_version")]
        public static string GameVersion { get; } = "1.0.0";

        [ScriptVar("game_difficulty")]
        public static int GameDifficulty { get; set; } = 1;

        [ScriptVar("is_debug_mode")]
        public static bool IsDebugMode { get; set; } = false;
    }

    /// <summary>
    /// 玩家类 - 用于演示对象属性和方法
    /// </summary>
    public class Player
    {
        // 属性
        public string Name { get; set; }
        public int Level { get; set; }
        public int Health { get; set; }
        public bool IsAlive { get; set; }

        // 构造函数
        public Player(string name, int level = 1)
        {
            Name = name;
            Level = level;
            Health = level * 10;
            IsAlive = true;
        }

        // 方法
        public string Get_Status()
        {
            return $"{Name} (Lv.{Level}) - HP: {Health}";
        }

        public void Take_Damage(int amount)
        {
            Health -= amount;
            if (Health <= 0)
            {
                Health = 0;
                IsAlive = false;
            }
        }

        public void Heal(int amount)
        {
            if (IsAlive)
            {
                Health += amount;
                Health = Math.Min(Health, Level * 10);
            }
        }
    }
}
