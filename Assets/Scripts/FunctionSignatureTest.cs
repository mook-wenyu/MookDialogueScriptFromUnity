using System;
using UnityEngine;
using MookDialogueScript;
using MookDialogueScript.Semantic.Contracts;
using MookDialogueScript.Semantic.Core;

/// <summary>
/// 测试新的 FunctionSignature 系统
/// </summary>
public class FunctionSignatureTest : MonoBehaviour
{
    void Start()
    {
        TestFunctionSignatureSystem();
    }

    void TestFunctionSignatureSystem()
    {
        Debug.Log("=== 测试 FunctionSignature 系统 ===");

        try
        {
            // 创建 FunctionManager
            var functionManager = new FunctionManager();

            // 注册一个测试函数
            functionManager.RegisterFunction("testFunc", new Func<double, string, bool>((num, text) => 
            {
                Debug.Log($"testFunc called with: {num}, {text}");
                return num > 0;
            }));

            // 获取函数签名
            var signature = functionManager.GetFunctionSignature("testFunc");
            if (signature != null)
            {
                Debug.Log($"函数签名获取成功: {signature.FormatSignature()}");
                Debug.Log($"最少参数: {signature.MinRequiredParameters}");
                Debug.Log($"最多参数: {signature.MaxParameters}");
                Debug.Log($"来源类型: {signature.SourceType}");
            }
            else
            {
                Debug.LogError("函数签名获取失败");
            }

            // 测试所有函数签名
            Debug.Log("所有注册的函数:");
            foreach (var kvp in functionManager.GetAllFunctionSignatures())
            {
                Debug.Log($"- {kvp.Key}: {kvp.Value.FormatSignature()}");
            }

            // 创建 SemanticAnalyzer 使用新架构
            var analyzer = new CompositeSemanticAnalyzer(null, null);
            Debug.Log("CompositeSemanticAnalyzer 创建成功");

            Debug.Log("=== FunctionSignature 系统测试完成 ===");
        }
        catch (Exception ex)
        {
            Debug.LogError($"测试过程中发生错误: {ex}");
        }
    }
}