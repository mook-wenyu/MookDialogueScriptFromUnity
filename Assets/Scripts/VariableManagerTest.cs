using System;
using UnityEngine;
using MookDialogueScript;

namespace MookDialogueScript.Tests
{
    /// <summary>
    /// 测试 VariableManager 的 context 参数改进
    /// </summary>
    public class VariableManagerTest : MonoBehaviour
    {
        [Header("测试配置")]
        [SerializeField] private bool runTestOnStart = true;
        
        /// <summary>
        /// 测试用的示例类
        /// </summary>
        public class TestObject
        {
            public string Name { get; set; } = "TestObject";
            public int Value { get; set; } = 42;
            public float Score = 100.0f; // 公共字段

            public string GetInfo()
            {
                return $"Name: {Name}, Value: {Value}, Score: {Score}";
            }

            public void SetValue(int newValue)
            {
                Value = newValue;
                Debug.Log($"TestObject.SetValue called with: {newValue}");
            }

            public void UpdateScore(float newScore)
            {
                Score = newScore;
                Debug.Log($"TestObject.UpdateScore called with: {newScore}");
            }
        }

        private void Start()
        {
            if (runTestOnStart)
            {
                RunTests();
            }
        }

        /// <summary>
        /// 运行所有测试
        /// </summary>
        [ContextMenu("运行测试")]
        public void RunTests()
        {
            Debug.Log("=== VariableManager Context 参数测试开始 ===");
            
            try
            {
                TestBasicMemberAccess();
                TestRegisteredObjectMemberAccess();
                TestVariableAccess();
                TestMethodReference();
                TestCompleteObjectRegistration();
                TestNamingConflicts();
                
                Debug.Log("=== 所有测试完成 ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"测试过程中出现错误: {ex}");
            }
        }

        /// <summary>
        /// 测试基础成员访问（无 context）
        /// </summary>
        private void TestBasicMemberAccess()
        {
            Debug.Log("--- 测试基础成员访问 ---");
            
            var variableManager = new VariableManager();
            var testObj = new TestObject { Name = "BasicTest", Value = 100 };
            var target = new RuntimeValue(testObj);
            
            // 测试属性访问
            var nameResult = variableManager.GetObjectMember(target, "Name", null);
            Debug.Log($"基础访问 Name: {nameResult} (期望: BasicTest)");
            
            var valueResult = variableManager.GetObjectMember(target, "Value", null);
            Debug.Log($"基础访问 Value: {valueResult} (期望: 100)");
        }

        /// <summary>
        /// 测试注册对象的成员访问（有 context）
        /// </summary>
        private void TestRegisteredObjectMemberAccess()
        {
            Debug.Log("--- 测试注册对象成员访问 ---");

            var context = new DialogueContext();
            var testObj = new TestObject { Name = "RegisteredTest", Value = 200 };

            // 注册对象
            context.RegisterObject("testObj", testObj);

            var target = new RuntimeValue(testObj);

            // 测试新的点号格式
            var nameResult = context.GetVariable("testObj.Name");
            Debug.Log($"注册对象变量访问 testObj.Name: {nameResult} (期望: RegisteredTest)");

            var valueResult = context.GetVariable("testObj.Value");
            Debug.Log($"注册对象变量访问 testObj.Value: {valueResult} (期望: 200)");
        }

        /// <summary>
        /// 测试变量访问
        /// </summary>
        private void TestVariableAccess()
        {
            Debug.Log("--- 测试变量访问 ---");
            
            var context = new DialogueContext();
            var variableManager = new VariableManager();
            var testObj = new TestObject { Name = "VariableTest", Value = 300 };
            
            // 注册对象（这会自动注册其属性为变量）
            context.RegisterObjectOnlyPropertiesAndFields("varTest", testObj);
            
            var target = new RuntimeValue(testObj);
            
            // 使用 context 进行成员访问
            var result = variableManager.GetObjectMember(target, "Name", context);
            Debug.Log($"通过 context 访问成员: {result}");
        }

        /// <summary>
        /// 测试方法引用
        /// </summary>
        private void TestMethodReference()
        {
            Debug.Log("--- 测试方法引用 ---");

            var context = new DialogueContext();
            var testObj = new TestObject { Name = "MethodTest", Value = 400 };

            // 注册对象（这会自动注册其方法为函数）
            context.RegisterObject("methodTest", testObj);

            var target = new RuntimeValue(testObj);

            // 检查是否有注册的函数
            bool hasGetInfo = context.HasFunction("methodTest.GetInfo");
            bool hasSetValue = context.HasFunction("methodTest.SetValue");
            bool hasUpdateScore = context.HasFunction("methodTest.UpdateScore");

            Debug.Log($"注册的函数 - GetInfo: {hasGetInfo}, SetValue: {hasSetValue}, UpdateScore: {hasUpdateScore}");

            // 测试通过 VariableManager 获取方法引用
            var variableManager = new VariableManager();
            var methodResult = variableManager.GetObjectMember(target, "GetInfo", context);

            Debug.Log($"方法引用结果类型: {methodResult.Type}");
            if (methodResult.Type == RuntimeValue.ValueType.Object)
            {
                Debug.Log($"方法引用对象类型: {methodResult.Value?.GetType().Name}");
                if (methodResult.Value is MethodReference methodRef)
                {
                    Debug.Log($"方法引用详情: {methodRef}");
                }
            }
        }

        /// <summary>
        /// 测试 RegisterObject 的完整功能（函数、属性、字段一起注册）
        /// </summary>
        private void TestCompleteObjectRegistration()
        {
            Debug.Log("--- 测试完整对象注册 ---");

            var context = new DialogueContext();
            var testObj = new TestObject
            {
                Name = "CompleteTest",
                Value = 500,
                Score = 99.5f
            };

            // 使用 RegisterObject 一次性注册所有成员
            context.RegisterObject("completeTest", testObj);

            // 验证属性注册
            bool hasNameProperty = context.HasVariable("completeTest.Name");
            bool hasValueProperty = context.HasVariable("completeTest.Value");

            // 验证字段注册
            bool hasScoreField = context.HasVariable("completeTest.Score");

            // 验证方法注册
            bool hasGetInfoMethod = context.HasFunction("completeTest.GetInfo");
            bool hasSetValueMethod = context.HasFunction("completeTest.SetValue");
            bool hasUpdateScoreMethod = context.HasFunction("completeTest.UpdateScore");

            Debug.Log($"属性注册 - Name: {hasNameProperty}, Value: {hasValueProperty}");
            Debug.Log($"字段注册 - Score: {hasScoreField}");
            Debug.Log($"方法注册 - GetInfo: {hasGetInfoMethod}, SetValue: {hasSetValueMethod}, UpdateScore: {hasUpdateScoreMethod}");

            // 测试访问注册的成员
            if (hasNameProperty)
            {
                var nameValue = context.GetVariable("completeTest.Name");
                Debug.Log($"访问属性 Name: {nameValue}");
            }

            if (hasScoreField)
            {
                var scoreValue = context.GetVariable("completeTest.Score");
                Debug.Log($"访问字段 Score: {scoreValue}");
            }

            // 测试对象映射
            bool canGetObjectByName = context.TryGetObjectByName("completeTest", out var retrievedObj);
            bool canGetNameByObject = context.TryGetObjectName(testObj, out var retrievedName);

            Debug.Log($"对象映射 - 通过名称获取对象: {canGetObjectByName}, 通过对象获取名称: {canGetNameByObject}");
            if (canGetNameByObject)
            {
                Debug.Log($"对象名称: {retrievedName}");
            }
        }

        /// <summary>
        /// 测试命名冲突处理
        /// </summary>
        private void TestNamingConflicts()
        {
            Debug.Log("--- 测试命名冲突处理 ---");

            var context = new DialogueContext();

            // 创建一个有同名方法和属性的测试对象
            var testObj = new TestObject { Name = "ConflictTest", Value = 500 };

            // 注册对象（会同时注册方法和属性）
            context.RegisterObject("conflictTest", testObj);

            var target = new RuntimeValue(testObj);
            var variableManager = new VariableManager();

            // 测试访问同名成员（应该优先返回方法）
            var result = variableManager.GetObjectMember(target, "GetInfo", context);
            Debug.Log($"访问同名成员 GetInfo 的结果类型: {result.Type}");

            // 检查是否有命名冲突警告
            bool hasGetInfoFunction = context.HasFunction("conflictTest.GetInfo");
            bool hasGetInfoVariable = context.HasVariable("conflictTest.GetInfo");

            Debug.Log($"conflictTest.GetInfo - 函数: {hasGetInfoFunction}, 变量: {hasGetInfoVariable}");
        }

    }
}
