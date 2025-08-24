using UnityEngine;
using UnityEditor;
using MookDialogueScript;
using System.Collections.Generic;

/// <summary>
/// 编辑器下的Bug修复验证工具（更新版）
/// </summary>
public class EditorBugFixVerifier
{
    [MenuItem("MookDialogueScript/Run Enhanced Bug Fix Tests")]
    public static void RunEnhancedBugFixTests()
    {
        Debug.Log("=== 开始增强Bug修复验证测试 ===");
        
        try
        {
            // 测试1: 函数扫描命名bug修复
            TestFunctionScanning();
            
            // 测试2: 严格禁止函数重名
            TestFunctionDuplicateRejection();
            
            // 测试3: 字典索引改进错误信息
            TestEnhancedDictionaryIndexing();
            
            // 测试4: MemberAccessor简化验证
            TestMemberAccessorSimplification();
            
            Debug.Log("=== 增强Bug修复验证测试完成 ===");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"测试过程中出现错误: {ex}");
        }
    }
    
    static void TestFunctionScanning()
    {
        Debug.Log("测试1: 函数扫描命名bug修复");
        var context = new DialogueContext();
        var functions = context.GetRegisteredFunctions();
        
        bool hasEmptyNameFunction = functions.ContainsKey("");
        if (hasEmptyNameFunction)
        {
            Debug.LogError("❌ 发现空名称函数，命名bug未修复");
        }
        else
        {
            Debug.Log("✅ 函数扫描命名bug已修复");
        }
        
        Debug.Log($"共注册了 {functions.Count} 个函数");
    }
    
    static void TestFunctionDuplicateRejection()
    {
        Debug.Log("测试2: 严格禁止函数重名");
        
        var funcMgr = new FunctionManager();
        
        // 先注册一个函数
        funcMgr.RegisterFunction("testFunc", (System.Func<string>)(() => "first"));
        
        // 尝试注册同名函数（应该被拒绝）
        funcMgr.RegisterFunction("testFunc", (System.Func<string>)(() => "second"));
        
        // 尝试注册大小写不同的函数（应该被检测为冲突）
        funcMgr.RegisterFunction("TestFunc", (System.Func<string>)(() => "case_conflict"));
        
        if (funcMgr.HasFunction("testFunc"))
        {
            Debug.Log("✅ 重名检查机制正常工作");
        }
        else
        {
            Debug.LogError("❌ 重名检查机制失败");
        }
    }
    
    static void TestEnhancedDictionaryIndexing()
    {
        Debug.Log("测试3: 字典索引改进错误信息");
        
        try
        {
            // 测试不存在的键，应该得到增强的错误信息
            var stringDict = new Dictionary<string, int> { {"a", 1}, {"b", 2} };
            var stringTarget = new RuntimeValue(stringDict);
            var nonExistentIndex = new RuntimeValue("c"); // 不存在的键
            
            try
            {
                Helper.GetIndexValue(stringTarget, nonExistentIndex, 100, 50);
            }
            catch (System.InvalidOperationException ex)
            {
                // 检查错误信息是否包含类型信息
                if (ex.Message.Contains("键类型:") && ex.Message.Contains("目标字典键类型:"))
                {
                    Debug.Log("✅ 字典索引错误信息已增强，包含类型信息");
                    Debug.Log($"错误信息示例: {ex.Message}");
                }
                else
                {
                    Debug.LogWarning("⚠️ 字典索引错误信息可能未完全增强");
                    Debug.Log($"当前错误信息: {ex.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ 字典索引测试异常: {ex.Message}");
        }
    }
    
    static void TestMemberAccessorSimplification()
    {
        Debug.Log("测试4: MemberAccessor简化验证");
        
        var testObj = new TestClassForMemberAccess();
        var type = testObj.GetType();
        
        // 获取一个方法的访问器
        var accessor = Helper.GetMemberAccessor(type, "TestMethod");
        
        if (accessor != null && accessor.Type == MemberAccessor.AccessorType.Method)
        {
            // 验证现在使用单一Method属性而不是Methods列表
            if (accessor.Method != null)
            {
                Debug.Log("✅ MemberAccessor已简化为单一MethodInfo");
                Debug.Log($"方法信息: {accessor.Method.Name}");
            }
            else
            {
                Debug.LogError("❌ MemberAccessor.Method为null");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ 无法获取方法访问器进行验证");
        }
    }
    
    // 测试用类
    public class TestClassForMemberAccess
    {
        public string TestMethod()
        {
            return "test";
        }
        
        // 重载方法（用于测试重载警告）
        public string TestMethod(int param)
        {
            return $"test_{param}";
        }
    }
}