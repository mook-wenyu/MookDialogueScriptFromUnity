using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MookDialogueScript
{
    /// <summary>
    /// 语义分析器类型系统
    /// </summary>
    public enum TypeKind
    {
        // 基元类型
        Number,
        String, 
        Boolean,
        Null,
        
        // 复合类型
        Object,
        Array,
        Dictionary,
        Function,
        
        // 特殊类型
        Any,  // 未知类型
        Error // 错误类型
    }

    /// <summary>
    /// 类型信息
    /// </summary>
    public class TypeInfo
    {
        /// <summary>
        /// 类型类别
        /// </summary>
        public TypeKind Kind { get; }
        
        /// <summary>
        /// CLR 类型（用于 Object 类型）
        /// </summary>
        public Type ClrType { get; }
        
        /// <summary>
        /// 元素类型（用于 Array 类型）
        /// </summary>
        public TypeInfo ElementType { get; }
        
        /// <summary>
        /// 键类型（用于 Dictionary 类型）
        /// </summary>
        public TypeInfo KeyType { get; }
        
        /// <summary>
        /// 值类型（用于 Dictionary 类型）
        /// </summary>
        public TypeInfo ValueType { get; }

        private TypeInfo(TypeKind kind, Type clrType = null, TypeInfo elementType = null, TypeInfo keyType = null, TypeInfo valueType = null)
        {
            Kind = kind;
            ClrType = clrType;
            ElementType = elementType;
            KeyType = keyType;
            ValueType = valueType;
        }

        // 静态工厂方法
        public static TypeInfo Number => new TypeInfo(TypeKind.Number, typeof(double));
        public static TypeInfo String => new TypeInfo(TypeKind.String, typeof(string));
        public static TypeInfo Boolean => new TypeInfo(TypeKind.Boolean, typeof(bool));
        public static TypeInfo Null => new TypeInfo(TypeKind.Null);
        public static TypeInfo Function => new TypeInfo(TypeKind.Function);
        public static TypeInfo Any => new TypeInfo(TypeKind.Any);
        public static TypeInfo Error => new TypeInfo(TypeKind.Error);
        
        public static TypeInfo Object(Type clrType) => new TypeInfo(TypeKind.Object, clrType);
        public static TypeInfo Array(TypeInfo elementType) => new TypeInfo(TypeKind.Array, elementType: elementType);
        public static TypeInfo Dictionary(TypeInfo keyType, TypeInfo valueType) => new TypeInfo(TypeKind.Dictionary, keyType: keyType, valueType: valueType);

        public override string ToString()
        {
            switch (Kind)
            {
                case TypeKind.Object:
                    return $"Object({ClrType?.Name ?? "unknown"})";
                case TypeKind.Array:
                    return $"Array({ElementType})";
                case TypeKind.Dictionary:
                    return $"Dictionary({KeyType}, {ValueType})";
                default:
                    return Kind.ToString();
            }
        }
    }

    /// <summary>
    /// 类型推断器
    /// </summary>
    public static class TypeInference
    {
        /// <summary>
        /// 从 CLR 类型映射到 TypeInfo
        /// </summary>
        public static TypeInfo FromClrType(Type clrType)
        {
            if (clrType == null)
                return TypeInfo.Null;

            // 基础类型映射
            if (clrType == typeof(double) || clrType == typeof(float) || clrType == typeof(int) || 
                clrType == typeof(long) || clrType == typeof(short) || clrType == typeof(byte) ||
                clrType == typeof(decimal))
                return TypeInfo.Number;

            if (clrType == typeof(string))
                return TypeInfo.String;

            if (clrType == typeof(bool))
                return TypeInfo.Boolean;

            // 数组类型
            if (clrType.IsArray)
            {
                var elementType = FromClrType(clrType.GetElementType());
                return TypeInfo.Array(elementType);
            }

            // 泛型集合类型
            if (clrType.IsGenericType)
            {
                var genericDef = clrType.GetGenericTypeDefinition();
                var genericArgs = clrType.GetGenericArguments();

                // List<T>, IList<T>, ICollection<T>, IEnumerable<T>
                if (genericDef == typeof(List<>) || 
                    genericDef == typeof(IList<>) || 
                    genericDef == typeof(ICollection<>) || 
                    genericDef == typeof(IEnumerable<>))
                {
                    var elementType = FromClrType(genericArgs[0]);
                    return TypeInfo.Array(elementType);
                }

                // Dictionary<K,V>, IDictionary<K,V>
                if (genericDef == typeof(Dictionary<,>) || 
                    genericDef == typeof(IDictionary<,>))
                {
                    var keyType = FromClrType(genericArgs[0]);
                    var valueType = FromClrType(genericArgs[1]);
                    return TypeInfo.Dictionary(keyType, valueType);
                }
            }

            // 函数类型
            if (typeof(Delegate).IsAssignableFrom(clrType))
                return TypeInfo.Function;

            // 其他对象类型
            return TypeInfo.Object(clrType);
        }

        /// <summary>
        /// 从 AST 节点推断类型
        /// </summary>
        public static TypeInfo InferType(ASTNode node, ISymbolTable symbolTable)
        {
            switch (node)
            {
                case NumberNode _:
                    return TypeInfo.Number;

                case BooleanNode _:
                    return TypeInfo.Boolean;

                case StringInterpolationExpressionNode _:
                    return TypeInfo.String;

                case VariableNode varNode:
                    return symbolTable?.GetVariableType(varNode.Name) ?? TypeInfo.Any;

                case IdentifierNode idNode:
                    return symbolTable?.GetIdentifierType(idNode.Name) ?? TypeInfo.Any;

                case BinaryOpNode binaryNode:
                    return InferBinaryOpType(binaryNode, symbolTable);

                case UnaryOpNode unaryNode:
                    return InferUnaryOpType(unaryNode, symbolTable);

                case CallExpressionNode callNode:
                    return InferCallType(callNode, symbolTable);

                case MemberAccessNode memberNode:
                    return InferMemberAccessType(memberNode, symbolTable);

                case IndexAccessNode indexNode:
                    return InferIndexAccessType(indexNode, symbolTable);

                default:
                    return TypeInfo.Any;
            }
        }

        private static TypeInfo InferBinaryOpType(BinaryOpNode node, ISymbolTable symbolTable)
        {
            var leftType = InferType(node.Left, symbolTable);
            var rightType = InferType(node.Right, symbolTable);

            switch (node.Operator)
            {
                case "+":
                case "-":
                case "*":
                case "/":
                case "%":
                    return TypeInfo.Number;

                case ">":
                case "<":
                case ">=":
                case "<=":
                case "==":
                case "!=":
                    return TypeInfo.Boolean;

                case "&&":
                case "||":
                case "xor":
                    return TypeInfo.Boolean;

                default:
                    return TypeInfo.Any;
            }
        }

        private static TypeInfo InferUnaryOpType(UnaryOpNode node, ISymbolTable symbolTable)
        {
            var operandType = InferType(node.Operand, symbolTable);

            switch (node.Operator)
            {
                case "-":
                    return TypeInfo.Number;
                case "not":
                    return TypeInfo.Boolean;
                default:
                    return operandType;
            }
        }

        private static TypeInfo InferCallType(CallExpressionNode node, ISymbolTable symbolTable)
        {
            var calleeType = InferType(node.Callee, symbolTable);
            
            if (calleeType.Kind == TypeKind.Function)
            {
                // 函数调用返回类型默认为 Any，可通过签名推断优化
                return TypeInfo.Any;
            }

            // 字符串调用（通过函数名）
            if (node.Callee is IdentifierNode identifierNode)
            {
                var functionInfo = symbolTable?.GetFunctionInfo(identifierNode.Name);
                return functionInfo?.ReturnType ?? TypeInfo.Any;
            }

            return TypeInfo.Error;
        }

        private static TypeInfo InferMemberAccessType(MemberAccessNode node, ISymbolTable symbolTable)
        {
            var targetType = InferType(node.Target, symbolTable);
            
            if (targetType.Kind == TypeKind.Object && targetType.ClrType != null)
            {
                var memberInfo = GetMemberInfo(targetType.ClrType, node.MemberName);
                if (memberInfo != null)
                {
                    return FromClrType(memberInfo.MemberType);
                }
            }

            return targetType.Kind == TypeKind.Any ? TypeInfo.Any : TypeInfo.Error;
        }

        private static TypeInfo InferIndexAccessType(IndexAccessNode node, ISymbolTable symbolTable)
        {
            var targetType = InferType(node.Target, symbolTable);
            var indexType = InferType(node.Index, symbolTable);

            switch (targetType.Kind)
            {
                case TypeKind.String:
                    return TypeInfo.String;

                case TypeKind.Array:
                    return targetType.ElementType ?? TypeInfo.Any;

                case TypeKind.Dictionary:
                    return targetType.ValueType ?? TypeInfo.Any;

                case TypeKind.Any:
                    return TypeInfo.Any;

                default:
                    return TypeInfo.Error;
            }
        }

        private static MemberInfo GetMemberInfo(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;
            
            // 查找属性
            var property = type.GetProperty(memberName, flags);
            if (property != null)
                return new MemberInfo(property.PropertyType, MemberKind.Property, property.CanWrite);

            // 查找字段
            var field = type.GetField(memberName, flags);
            if (field != null)
                return new MemberInfo(field.FieldType, MemberKind.Field, !field.IsInitOnly);

            // 查找方法
            var methods = type.GetMethods(flags).Where(m => string.Equals(m.Name, memberName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (methods.Length > 0)
                return new MemberInfo(typeof(Delegate), MemberKind.Method, false);

            return null;
        }
    }

    /// <summary>
    /// 成员信息
    /// </summary>
    public class MemberInfo
    {
        public Type MemberType { get; }
        public MemberKind Kind { get; }
        public bool CanWrite { get; }

        public MemberInfo(Type memberType, MemberKind kind, bool canWrite)
        {
            MemberType = memberType;
            Kind = kind;
            CanWrite = canWrite;
        }
    }

    /// <summary>
    /// 成员类型
    /// </summary>
    public enum MemberKind
    {
        Property,
        Field,
        Method
    }

    /// <summary>
    /// 函数信息
    /// </summary>
    public class FunctionInfo
    {
        public string Name { get; }
        public TypeInfo ReturnType { get; }
        public List<TypeInfo> ParameterTypes { get; }
        public int MinParameters { get; }
        public int MaxParameters { get; }

        public FunctionInfo(string name, TypeInfo returnType, List<TypeInfo> parameterTypes, int minParameters = -1, int maxParameters = -1)
        {
            Name = name;
            ReturnType = returnType;
            ParameterTypes = parameterTypes ?? new List<TypeInfo>();
            MinParameters = minParameters >= 0 ? minParameters : ParameterTypes.Count;
            MaxParameters = maxParameters >= 0 ? maxParameters : ParameterTypes.Count;
        }
    }

    /// <summary>
    /// 符号表接口
    /// </summary>
    public interface ISymbolTable
    {
        TypeInfo GetVariableType(string name);
        TypeInfo GetIdentifierType(string name);
        FunctionInfo GetFunctionInfo(string name);
        bool IsDefined(string name);
        void DefineVariable(string name, TypeInfo type);
        void DefineFunction(string name, FunctionInfo info);
    }

    /// <summary>
    /// 符号表实现
    /// </summary>
    public class SymbolTable : ISymbolTable
    {
        private readonly Dictionary<string, TypeInfo> _variables = new Dictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FunctionInfo> _functions = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly ISymbolTable _parent;

        public SymbolTable(ISymbolTable parent = null)
        {
            _parent = parent;
        }

        public TypeInfo GetVariableType(string name)
        {
            if (_variables.TryGetValue(name, out var type))
                return type;
            
            return _parent?.GetVariableType(name) ?? TypeInfo.Error;
        }

        public TypeInfo GetIdentifierType(string name)
        {
            // 先查找变量
            var varType = GetVariableType(name);
            if (varType.Kind != TypeKind.Error)
                return varType;

            // 再查找函数
            var funcInfo = GetFunctionInfo(name);
            if (funcInfo != null)
                return TypeInfo.Function;

            return TypeInfo.Error;
        }

        public FunctionInfo GetFunctionInfo(string name)
        {
            if (_functions.TryGetValue(name, out var info))
                return info;
            
            return _parent?.GetFunctionInfo(name);
        }

        public bool IsDefined(string name)
        {
            return _variables.ContainsKey(name) || _functions.ContainsKey(name) || (_parent?.IsDefined(name) ?? false);
        }

        public void DefineVariable(string name, TypeInfo type)
        {
            _variables[name] = type;
        }

        public void DefineFunction(string name, FunctionInfo info)
        {
            _functions[name] = info;
        }

        /// <summary>
        /// 获取当前作用域中的所有变量名
        /// </summary>
        public IEnumerable<string> GetLocalVariableNames()
        {
            return _variables.Keys;
        }

        /// <summary>
        /// 获取当前作用域中的所有函数名
        /// </summary>
        public IEnumerable<string> GetLocalFunctionNames()
        {
            return _functions.Keys;
        }

        /// <summary>
        /// 检查变量名是否在当前作用域定义
        /// </summary>
        public bool IsLocallyDefined(string name)
        {
            return _variables.ContainsKey(name) || _functions.ContainsKey(name);
        }
    }

    /// <summary>
    /// 全局符号表
    /// </summary>
    public class GlobalSymbolTable : SymbolTable
    {
        private readonly Dictionary<string, string> _nodeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly VariableManager _variableManager;
        private readonly FunctionManager _functionManager;

        public GlobalSymbolTable(VariableManager variableManager, FunctionManager functionManager) : base(null)
        {
            _variableManager = variableManager;
            _functionManager = functionManager;
            
            InitializeBuiltInSymbols();
        }

        /// <summary>
        /// 初始化内置符号
        /// </summary>
        private void InitializeBuiltInSymbols()
        {
            // 从 VariableManager 获取内置变量
            if (_variableManager != null)
            {
                var variables = _variableManager.GetAllVariables();
                foreach (var kvp in variables)
                {
                    var typeInfo = TypeInference.FromClrType(kvp.Value.GetType());
                    DefineVariable(kvp.Key, typeInfo);
                }
            }

            // 从 FunctionManager 获取内置函数
            if (_functionManager != null)
            {
                var functionNames = _functionManager.GetAllFunctionNames();
                foreach (var name in functionNames)
                {
                    var functionInfo = CreateFunctionInfoFromManager(name);
                    if (functionInfo != null)
                        DefineFunction(name, functionInfo);
                }
            }
        }

        private FunctionInfo CreateFunctionInfoFromManager(string name)
        {
            // 这里可以通过反射获取更详细的函数签名信息
            // 暂时返回基本的函数信息
            return new FunctionInfo(name, TypeInfo.Any, new List<TypeInfo>());
        }

        /// <summary>
        /// 添加节点名
        /// </summary>
        public void AddNodeName(string nodeName)
        {
            _nodeNames[nodeName] = nodeName;
        }

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        public bool NodeExists(string nodeName)
        {
            return _nodeNames.ContainsKey(nodeName);
        }

        /// <summary>
        /// 获取相似节点名建议
        /// </summary>
        public string GetSimilarNodeName(string nodeName)
        {
            return _nodeNames.Keys
                .Where(name => LevenshteinDistance(name.ToLower(), nodeName.ToLower()) <= 2)
                .OrderBy(name => LevenshteinDistance(name.ToLower(), nodeName.ToLower()))
                .FirstOrDefault();
        }

        /// <summary>
        /// 计算编辑距离（用于相似名称建议）
        /// </summary>
        private static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return string.IsNullOrEmpty(target) ? 0 : target.Length;

            if (string.IsNullOrEmpty(target))
                return source.Length;

            var matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source.Length, target.Length];
        }

        /// <summary>
        /// 获取所有节点名
        /// </summary>
        public IEnumerable<string> GetAllNodeNames()
        {
            return _nodeNames.Keys;
        }
    }

    /// <summary>
    /// 作用域管理器
    /// </summary>
    public class ScopeManager
    {
        private readonly Stack<SymbolTable> _scopes = new Stack<SymbolTable>();
        private readonly GlobalSymbolTable _globalScope;

        public ScopeManager(GlobalSymbolTable globalScope)
        {
            _globalScope = globalScope;
            _scopes.Push(globalScope);
        }

        /// <summary>
        /// 当前符号表
        /// </summary>
        public ISymbolTable CurrentScope => _scopes.Peek();

        /// <summary>
        /// 全局符号表
        /// </summary>
        public GlobalSymbolTable GlobalScope => _globalScope;

        /// <summary>
        /// 进入新作用域
        /// </summary>
        public void EnterScope()
        {
            var currentScope = _scopes.Peek();
            var newScope = new SymbolTable(currentScope);
            _scopes.Push(newScope);
        }

        /// <summary>
        /// 退出当前作用域
        /// </summary>
        public void ExitScope()
        {
            if (_scopes.Count > 1) // 保留全局作用域
            {
                _scopes.Pop();
            }
        }

        /// <summary>
        /// 定义变量到当前作用域
        /// </summary>
        public void DefineVariable(string name, TypeInfo type)
        {
            if (_scopes.Peek() is SymbolTable symbolTable)
            {
                symbolTable.DefineVariable(name, type);
            }
        }

        /// <summary>
        /// 定义函数到全局作用域
        /// </summary>
        public void DefineFunction(string name, FunctionInfo info)
        {
            _globalScope.DefineFunction(name, info);
        }

        /// <summary>
        /// 检查当前作用域是否已定义变量
        /// </summary>
        public bool IsLocallyDefined(string name)
        {
            return _scopes.Peek() is SymbolTable symbolTable && symbolTable.IsLocallyDefined(name);
        }
    }

    /// <summary>
    /// 语义诊断严重性级别
    /// </summary>
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 语义诊断信息
    /// </summary>
    public class Diagnostic
    {
        /// <summary>
        /// 诊断代码
        /// </summary>
        public string Code { get; set; }
        
        /// <summary>
        /// 严重性级别
        /// </summary>
        public DiagnosticSeverity Severity { get; set; }
        
        /// <summary>
        /// 诊断消息
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 行号
        /// </summary>
        public int Line { get; set; }
        
        /// <summary>
        /// 列号
        /// </summary>
        public int Column { get; set; }
        
        /// <summary>
        /// 修复建议
        /// </summary>
        public string Suggestion { get; set; }

        public Diagnostic(string code, DiagnosticSeverity severity, string message, int line = 0, int column = 0, string suggestion = null)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Line = line;
            Column = column;
            Suggestion = suggestion;
        }
    }

    /// <summary>
    /// 语义分析配置选项
    /// </summary>
    public class AnalysisOptions
    {
        /// <summary>
        /// 大小写不一致警告（默认开启）
        /// </summary>
        public bool CaseInconsistencyAsWarning { get; set; } = true;
        
        /// <summary>
        /// 数值当布尔使用的处理方式（默认警告）
        /// </summary>
        public DiagnosticSeverity NumberAsBooleanSeverity { get; set; } = DiagnosticSeverity.Warning;
        
        /// <summary>
        /// 字符串拼接非法类型的处理方式（默认警告）
        /// </summary>
        public DiagnosticSeverity InvalidStringConcatSeverity { get; set; } = DiagnosticSeverity.Warning;
        
        /// <summary>
        /// 未知类型参与运算的处理方式（默认警告）
        /// </summary>
        public DiagnosticSeverity UnknownTypeOperationSeverity { get; set; } = DiagnosticSeverity.Warning;
        
        /// <summary>
        /// 自跳转警告（默认关闭）
        /// </summary>
        public bool SelfJumpAsWarning { get; set; } = false;
        
        /// <summary>
        /// 标签严格性检查（默认关闭）
        /// </summary>
        public bool StrictTagValidation { get; set; } = false;
        
        /// <summary>
        /// 启用函数签名推断（默认开启）
        /// </summary>
        public bool EnableSignatureInference { get; set; } = true;
    }

    /// <summary>
    /// 语义分析报告
    /// </summary>
    public class SemanticReport
    {
        /// <summary>
        /// 所有诊断信息
        /// </summary>
        public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();
        
        /// <summary>
        /// 是否存在错误
        /// </summary>
        public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        
        /// <summary>
        /// 是否存在警告
        /// </summary>
        public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);
        
        /// <summary>
        /// 错误数量
        /// </summary>
        public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        
        /// <summary>
        /// 警告数量
        /// </summary>
        public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        
        /// <summary>
        /// 信息数量
        /// </summary>
        public int InfoCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);

        /// <summary>
        /// 添加诊断信息
        /// </summary>
        public void AddDiagnostic(Diagnostic diagnostic)
        {
            Diagnostics.Add(diagnostic);
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        public void AddError(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Error, message, line, column, suggestion));
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Warning, message, line, column, suggestion));
        }

        /// <summary>
        /// 添加信息
        /// </summary>
        public void AddInfo(string code, string message, int line = 0, int column = 0, string suggestion = null)
        {
            AddDiagnostic(new Diagnostic(code, DiagnosticSeverity.Info, message, line, column, suggestion));
        }
    }

    /// <summary>
    /// 全局节点查找提供者接口
    /// </summary>
    public interface IGlobalNodeProvider
    {
        /// <summary>
        /// 查找节点是否存在
        /// </summary>
        bool NodeExists(string nodeName);
        
        /// <summary>
        /// 获取相似节点名建议
        /// </summary>
        string GetSimilarNodeName(string nodeName);
    }

    /// <summary>
    /// 语义分析诊断代码常量
    /// </summary>
    public static class SemanticDiagnosticCodes
    {
        // 变量相关错误
        public const string SEM001 = "SEM001"; // 未定义变量
        public const string SEM002 = "SEM002"; // 覆盖内置变量/重复定义
        public const string SEM003 = "SEM003"; // 未知变量操作
        public const string SEM004 = "SEM004"; // 只读变量修改
        public const string SEM005 = "SEM005"; // 变量类型不匹配

        // 函数调用相关错误
        public const string SEM010 = "SEM010"; // 不可调用的值
        public const string SEM011 = "SEM011"; // 未知函数名
        public const string SEM012 = "SEM012"; // 函数名大小写不一致
        public const string SEM013 = "SEM013"; // 参数计数不匹配
        public const string SEM014 = "SEM014"; // 参数类型不兼容

        // 成员访问相关错误
        public const string SEM020 = "SEM020"; // 成员不存在
        public const string SEM021 = "SEM021"; // 成员大小写不一致

        // 索引访问相关错误
        public const string SEM030 = "SEM030"; // 不可索引类型/键类型不匹配

        // 条件表达式相关错误
        public const string SEM040 = "SEM040"; // 条件非布尔
        public const string SEM041 = "SEM041"; // 数值充当布尔

        // 运算符相关错误
        public const string SEM050 = "SEM050"; // 算数类型错误
        public const string SEM051 = "SEM051"; // 常量除零/比较不同类型

        // 跳转相关错误
        public const string SEM060 = "SEM060"; // 跳转目标不存在
        public const string SEM061 = "SEM061"; // 跳转大小写不一致/字符串插值类型错误
        public const string SEM062 = "SEM062"; // 自跳转

        // 结构相关错误
        public const string SEM070 = "SEM070"; // 重复节点名/未知类型操作

        // 通用错误
        public const string SEM999 = "SEM999"; // 其他未处理错误
    }
    public class SemanticAnalyzer
    {
        private readonly AnalysisOptions _options;
        private readonly IGlobalNodeProvider _nodeProvider;
        private SemanticReport _report;
        
        // 缓存机制
        private readonly Dictionary<int, SemanticReport> _reportCache = new();
        private int _lastManagersHashCode = 0;
        
        public SemanticAnalyzer(AnalysisOptions options = null, IGlobalNodeProvider nodeProvider = null)
        {
            _options = options ?? new AnalysisOptions();
            _nodeProvider = nodeProvider;
        }

        /// <summary>
        /// 分析脚本语义（支持缓存）
        /// </summary>
        public SemanticReport Analyze(ScriptNode script, VariableManager variableManager = null, FunctionManager functionManager = null)
        {
            if (script == null)
            {
                var errorReport = new SemanticReport();
                errorReport.AddError("SEM000", "脚本为空");
                return errorReport;
            }

            // 计算缓存键
            var scriptHash = GetScriptHash(script);
            var managersHash = GetManagersHash(variableManager, functionManager);
            var cacheKey = HashCode.Combine(scriptHash, managersHash);

            // 检查缓存
            if (_reportCache.TryGetValue(cacheKey, out var cachedReport) && 
                managersHash == _lastManagersHashCode)
            {
                return cachedReport;
            }

            // 执行语义分析
            _report = new SemanticReport();
            _lastManagersHashCode = managersHash;

            // 创建符号表管理器
            var globalSymbolTable = new GlobalSymbolTable(variableManager, functionManager);
            var scopeManager = new ScopeManager(globalSymbolTable);

            // 第一遍：收集所有节点名
            CollectNodeNames(script, globalSymbolTable);

            // 第二遍：分析每个节点
            foreach (var node in script.Nodes)
            {
                AnalyzeNodeDefinition(node, scopeManager);
            }
            
            // 缓存结果
            _reportCache[cacheKey] = _report;
            
            // 清理旧缓存（保持缓存大小合理）
            if (_reportCache.Count > 100)
            {
                var oldestKeys = _reportCache.Keys.Take(_reportCache.Count - 50).ToList();
                foreach (var key in oldestKeys)
                {
                    _reportCache.Remove(key);
                }
            }
            
            return _report;
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public void ClearCache()
        {
            _reportCache.Clear();
            _lastManagersHashCode = 0;
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int CachedReports, int TotalMemoryUsage) GetCacheStats()
        {
            var memoryUsage = _reportCache.Sum(kvp => 
                kvp.Value.Diagnostics.Count * 100 + // 粗略估算每个诊断信息100字节
                kvp.Key.GetHashCode().ToString().Length * 2); // 键的内存使用
                
            return (_reportCache.Count, memoryUsage);
        }

        /// <summary>
        /// 计算脚本的哈希值
        /// </summary>
        private int GetScriptHash(ScriptNode script)
        {
            unchecked
            {
                int hash = 17;
                foreach (var node in script.Nodes)
                {
                    hash = hash * 31 + GetNodeHash(node);
                }
                return hash;
            }
        }

        /// <summary>
        /// 计算节点的哈希值
        /// </summary>
        private int GetNodeHash(NodeDefinitionNode node)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (node.Name?.GetHashCode() ?? 0);
                hash = hash * 31 + node.Content.Count;
                
                // 简化的内容哈希
                foreach (var content in node.Content.Take(5)) // 只取前5个内容节点避免过度计算
                {
                    hash = hash * 31 + content.GetType().GetHashCode();
                    hash = hash * 31 + content.Line;
                }
                
                return hash;
            }
        }

        /// <summary>
        /// 计算管理器的哈希值
        /// </summary>
        private int GetManagersHash(VariableManager variableManager, FunctionManager functionManager)
        {
            unchecked
            {
                int hash = 17;
                
                // 变量管理器的版本哈希
                if (variableManager != null)
                {
                    var variables = variableManager.GetAllVariables();
                    hash = hash * 31 + variables.Count;
                    
                    // 对变量名和类型进行哈希
                    foreach (var kvp in variables.Take(10)) // 限制数量避免性能问题
                    {
                        hash = hash * 31 + (kvp.Key?.GetHashCode() ?? 0);
                        hash = hash * 31 + (kvp.Value.Type.GetHashCode());
                    }
                }
                
                // 函数管理器的版本哈希
                if (functionManager != null)
                {
                    var functions = functionManager.GetAllFunctionNames().ToList();
                    hash = hash * 31 + functions.Count;
                    
                    // 对函数名进行哈希
                    foreach (var funcName in functions.Take(10)) // 限制数量避免性能问题
                    {
                        hash = hash * 31 + (funcName?.GetHashCode() ?? 0);
                    }
                }
                
                return hash;
            }
        }

        /// <summary>
        /// 收集所有节点名
        /// </summary>
        private void CollectNodeNames(ScriptNode script, GlobalSymbolTable globalSymbolTable)
        {
            foreach (var node in script.Nodes)
            {
                if (!string.IsNullOrEmpty(node.Name))
                {
                    // 检查重名
                    if (globalSymbolTable.NodeExists(node.Name))
                    {
                        _report.AddWarning("SEM070", $"节点名 '{node.Name}' 重复定义", node.Line, node.Column);
                    }
                    else
                    {
                        globalSymbolTable.AddNodeName(node.Name);
                    }
                }
            }
        }

        /// <summary>
        /// 分析节点定义
        /// </summary>
        private void AnalyzeNodeDefinition(NodeDefinitionNode node, ScopeManager scopeManager)
        {
            // 进入节点作用域
            scopeManager.EnterScope();

            try
            {
                // 分析节点内容
                foreach (var content in node.Content)
                {
                    AnalyzeContentNode(content, scopeManager);
                }
            }
            finally
            {
                // 退出节点作用域
                scopeManager.ExitScope();
            }
        }

        /// <summary>
        /// 分析内容节点
        /// </summary>
        private void AnalyzeContentNode(ContentNode content, ScopeManager scopeManager)
        {
            switch (content)
            {
                case VarCommandNode varCmd:
                    AnalyzeVarCommand(varCmd, scopeManager);
                    break;

                case ConditionNode condNode:
                    AnalyzeCondition(condNode, scopeManager);
                    break;

                case DialogueNode dialogueNode:
                    AnalyzeDialogue(dialogueNode, scopeManager);
                    break;

                case ChoiceNode choiceNode:
                    AnalyzeChoice(choiceNode, scopeManager);
                    break;

                case JumpCommandNode jumpCmd:
                    AnalyzeJumpCommand(jumpCmd, scopeManager);
                    break;

                case CallCommandNode callCmd:
                    AnalyzeCallCommand(callCmd, scopeManager);
                    break;

                // 其他节点类型...
                default:
                    // 对于未知类型，不报错，只记录信息
                    _report.AddInfo("SEM999", $"未处理的内容节点类型: {content.GetType().Name}", content.Line, content.Column);
                    break;
            }
        }

        /// <summary>
        /// 分析变量命令
        /// </summary>
        private void AnalyzeVarCommand(VarCommandNode varCmd, ScopeManager scopeManager)
        {
            var variableName = varCmd.VariableName;
            var operation = varCmd.Operation ?? "var"; // 默认为 var 操作
            
            // 推断右值类型
            var valueType = TypeInfo.Any;
            if (varCmd.Value != null)
            {
                valueType = TypeInference.InferType(varCmd.Value, scopeManager.CurrentScope);
                ValidateExpression(varCmd.Value, scopeManager);
            }

            switch (operation.ToLower())
            {
                case "var":
                    // 变量声明
                    HandleVariableDeclaration(variableName, valueType, varCmd, scopeManager);
                    break;

                case "set":
                    // 变量赋值 
                    HandleVariableAssignment(variableName, valueType, varCmd, scopeManager);
                    break;

                case "add":
                case "sub":
                case "mul":
                case "div":
                case "mod":
                    // 算术运算赋值
                    HandleArithmeticAssignment(variableName, valueType, operation, varCmd, scopeManager);
                    break;

                default:
                    _report.AddError(SemanticDiagnosticCodes.SEM003, $"未知的变量操作: {operation}", varCmd.Line, varCmd.Column);
                    break;
            }
        }

        /// <summary>
        /// 处理变量声明（var 操作）
        /// </summary>
        private void HandleVariableDeclaration(string variableName, TypeInfo valueType, VarCommandNode varCmd, ScopeManager scopeManager)
        {
            // 检查重名（同节点内）
            if (scopeManager.IsLocallyDefined(variableName))
            {
                _report.AddWarning(SemanticDiagnosticCodes.SEM002, $"变量 '{variableName}' 在当前节点中重复定义", varCmd.Line, varCmd.Column);
            }

            // 检查是否覆盖内置变量
            var globalScope = scopeManager.GlobalScope;
            if (globalScope.IsDefined(variableName) && !globalScope.IsLocallyDefined(variableName))
            {
                _report.AddError(SemanticDiagnosticCodes.SEM002, $"不能重定义内置变量 '{variableName}'", varCmd.Line, varCmd.Column);
                return;
            }

            // 定义变量
            scopeManager.DefineVariable(variableName, valueType);
        }

        /// <summary>
        /// 处理变量赋值（set 操作）
        /// </summary>
        private void HandleVariableAssignment(string variableName, TypeInfo valueType, VarCommandNode varCmd, ScopeManager scopeManager)
        {
            // 检查变量是否已定义
            if (!scopeManager.CurrentScope.IsDefined(variableName))
            {
                var suggestion = GetSimilarVariableName(variableName, scopeManager.CurrentScope);
                var suggestionText = suggestion != null ? $"，你是否想要使用 '{suggestion}'?" : "";
                _report.AddError(SemanticDiagnosticCodes.SEM001, $"未定义的变量 '{variableName}'{suggestionText}", varCmd.Line, varCmd.Column, suggestion);
                return;
            }

            // 检查是否为只读内置变量
            if (IsReadOnlyBuiltInVariable(variableName, scopeManager.GlobalScope))
            {
                _report.AddError(SemanticDiagnosticCodes.SEM004, $"不能修改只读变量 '{variableName}'", varCmd.Line, varCmd.Column);
                return;
            }

            // 变量类型兼容性检查（可选，根据配置决定严格程度）
            var existingType = scopeManager.CurrentScope.GetVariableType(variableName);
            if (existingType.Kind != TypeKind.Any && valueType.Kind != TypeKind.Any && 
                !IsTypeCompatible(valueType, existingType))
            {
                _report.AddWarning(SemanticDiagnosticCodes.SEM005, $"变量 '{variableName}' 类型不匹配：期望 {existingType}，但得到 {valueType}", varCmd.Line, varCmd.Column);
            }
        }

        /// <summary>
        /// 处理算术运算赋值（add, sub, mul, div, mod 操作）
        /// </summary>
        private void HandleArithmeticAssignment(string variableName, TypeInfo valueType, string operation, VarCommandNode varCmd, ScopeManager scopeManager)
        {
            // 检查变量是否已定义
            if (!scopeManager.CurrentScope.IsDefined(variableName))
            {
                var suggestion = GetSimilarVariableName(variableName, scopeManager.CurrentScope);
                var suggestionText = suggestion != null ? $"，你是否想要使用 '{suggestion}'?" : "";
                _report.AddError(SemanticDiagnosticCodes.SEM001, $"未定义的变量 '{variableName}'{suggestionText}", varCmd.Line, varCmd.Column, suggestion);
                return;
            }

            // 检查是否为只读内置变量
            if (IsReadOnlyBuiltInVariable(variableName, scopeManager.GlobalScope))
            {
                _report.AddError(SemanticDiagnosticCodes.SEM004, $"不能修改只读变量 '{variableName}'", varCmd.Line, varCmd.Column);
                return;
            }

            // 算术运算要求目标变量为数字类型
            var existingType = scopeManager.CurrentScope.GetVariableType(variableName);
            if (existingType.Kind != TypeKind.Number && existingType.Kind != TypeKind.Any)
            {
                _report.AddError(SemanticDiagnosticCodes.SEM050, $"算术运算 '{operation}' 要求目标变量为数字类型，但 '{variableName}' 是 {existingType}", varCmd.Line, varCmd.Column);
            }

            // 算术运算要求右值为数字类型
            if (valueType.Kind != TypeKind.Number && valueType.Kind != TypeKind.Any)
            {
                _report.AddError(SemanticDiagnosticCodes.SEM050, $"算术运算 '{operation}' 要求右值为数字类型，但得到 {valueType}", varCmd.Line, varCmd.Column);
            }

            // 检查除零和取模零
            if ((operation == "div" || operation == "mod") && varCmd.Value is NumberNode numberNode)
            {
                if (Math.Abs(numberNode.Value) < double.Epsilon)
                {
                    _report.AddError(SemanticDiagnosticCodes.SEM051, $"算术运算 '{operation}' 的右值不能为零", varCmd.Value.Line, varCmd.Value.Column);
                }
            }
        }

        /// <summary>
        /// 检查是否为只读内置变量
        /// </summary>
        private bool IsReadOnlyBuiltInVariable(string variableName, GlobalSymbolTable globalScope)
        {
            // 这里需要和 VariableManager 协作，检查变量是否只读
            // 简单实现：假设所有内置变量都是可写的，实际应该检查属性/字段的写权限
            return false;
        }

        /// <summary>
        /// 分析条件语句
        /// </summary>
        private void AnalyzeCondition(ConditionNode condNode, ScopeManager scopeManager)
        {
            if (condNode.Condition != null)
            {
                ValidateConditionExpression(condNode.Condition, scopeManager);
            }

            // 进入条件作用域
            scopeManager.EnterScope();
            try
            {
                // 分析条件内容
                foreach (var content in condNode.ThenContent)
                {
                    AnalyzeContentNode(content, scopeManager);
                }
            }
            finally
            {
                scopeManager.ExitScope();
            }

            // 分析 else 内容
            if (condNode.ElseContent != null && condNode.ElseContent.Count > 0)
            {
                scopeManager.EnterScope();
                try
                {
                    foreach (var content in condNode.ElseContent)
                    {
                        AnalyzeContentNode(content, scopeManager);
                    }
                }
                finally
                {
                    scopeManager.ExitScope();
                }
            }
        }

        /// <summary>
        /// 分析对话节点
        /// </summary>
        private void AnalyzeDialogue(DialogueNode dialogueNode, ScopeManager scopeManager)
        {
            // 分析文本中的插值表达式
            if (dialogueNode.Text != null)
            {
                AnalyzeStringInterpolation(dialogueNode.Text, scopeManager);
            }
        }

        /// <summary>
        /// 分析选择节点
        /// </summary>
        private void AnalyzeChoice(ChoiceNode choiceNode, ScopeManager scopeManager)
        {
            // 分析选择文本中的插值
            if (choiceNode.Text != null)
            {
                AnalyzeStringInterpolation(choiceNode.Text, scopeManager);
            }

            // 分析选择条件
            if (choiceNode.Condition != null)
            {
                ValidateConditionExpression(choiceNode.Condition, scopeManager);
            }
        }

        /// <summary>
        /// 分析跳转命令
        /// </summary>
        private void AnalyzeJumpCommand(JumpCommandNode jumpCmd, ScopeManager scopeManager)
        {
            var targetNode = jumpCmd.TargetNode;
            
            if (string.IsNullOrEmpty(targetNode))
            {
                _report.AddError("SEM060", "跳转目标不能为空", jumpCmd.Line, jumpCmd.Column);
                return;
            }

            // 检查目标节点是否存在
            if (!scopeManager.GlobalScope.NodeExists(targetNode))
            {
                var suggestion = scopeManager.GlobalScope.GetSimilarNodeName(targetNode);
                var suggestionText = suggestion != null ? $"，你是否想要跳转到 '{suggestion}'?" : "";
                _report.AddError("SEM060", $"跳转目标节点 '{targetNode}' 不存在{suggestionText}", jumpCmd.Line, jumpCmd.Column, suggestion);
                return;
            }

            // 检查大小写一致性
            var actualNodeName = scopeManager.GlobalScope.GetAllNodeNames()
                .FirstOrDefault(name => string.Equals(name, targetNode, StringComparison.OrdinalIgnoreCase));
            
            if (actualNodeName != null && !string.Equals(actualNodeName, targetNode, StringComparison.Ordinal))
            {
                if (_options.CaseInconsistencyAsWarning)
                {
                    _report.AddWarning("SEM061", $"跳转目标 '{targetNode}' 与实际节点名 '{actualNodeName}' 大小写不一致", 
                        jumpCmd.Line, jumpCmd.Column, $"使用 '{actualNodeName}'");
                }
            }

            // 检查自跳转
            if (_options.SelfJumpAsWarning && string.Equals(targetNode, GetCurrentNodeName(jumpCmd), StringComparison.OrdinalIgnoreCase))
            {
                _report.AddWarning("SEM062", $"检测到自跳转到 '{targetNode}'，可能导致无限循环", jumpCmd.Line, jumpCmd.Column);
            }
        }

        /// <summary>
        /// 分析函数调用命令
        /// </summary>
        private void AnalyzeCallCommand(CallCommandNode callCmd, ScopeManager scopeManager)
        {
            // 验证函数是否存在
            if (!scopeManager.GlobalScope.IsDefined(callCmd.FunctionName))
            {
                var suggestion = GetSimilarFunctionName(callCmd.FunctionName, scopeManager.CurrentScope);
                var suggestionText = suggestion != null ? $"，你是否想要调用 '{suggestion}'?" : "";
                _report.AddError("SEM010", $"未定义的函数 '{callCmd.FunctionName}'{suggestionText}", callCmd.Line, callCmd.Column, suggestion);
            }

            // 验证参数表达式
            foreach (var param in callCmd.Parameters)
            {
                ValidateExpression(param, scopeManager);
            }
        }

        /// <summary>
        /// 验证表达式
        /// </summary>
        private TypeInfo ValidateExpression(ExpressionNode expr, ScopeManager scopeManager)
        {
            return ValidateExpressionInternal(expr, scopeManager.CurrentScope);
        }

        /// <summary>
        /// 内部表达式验证方法
        /// </summary>
        private TypeInfo ValidateExpressionInternal(ExpressionNode expr, ISymbolTable symbolTable)
        {
            var inferredType = TypeInference.InferType(expr, symbolTable);

            switch (expr)
            {
                case BinaryOpNode binaryNode:
                    return ValidateBinaryOperation(binaryNode, symbolTable);

                case UnaryOpNode unaryNode:
                    return ValidateUnaryOperation(unaryNode, symbolTable);

                case CallExpressionNode callNode:
                    return ValidateCallExpression(callNode, symbolTable);

                case MemberAccessNode memberNode:
                    return ValidateMemberAccess(memberNode, symbolTable);

                case IndexAccessNode indexNode:
                    return ValidateIndexAccess(indexNode, symbolTable);

                case VariableNode varNode:
                    return ValidateVariable(varNode, symbolTable);

                case IdentifierNode idNode:
                    return ValidateIdentifier(idNode, symbolTable);

                // 字面量节点直接返回推断类型
                case NumberNode _:
                case BooleanNode _:
                case StringInterpolationExpressionNode _:
                    return inferredType;

                default:
                    return inferredType;
            }
        }

        /// <summary>
        /// 验证二元运算
        /// </summary>
        private TypeInfo ValidateBinaryOperation(BinaryOpNode node, ISymbolTable symbolTable)
        {
            var leftType = ValidateExpressionInternal(node.Left, symbolTable);
            var rightType = ValidateExpressionInternal(node.Right, symbolTable);

            switch (node.Operator)
            {
                case "+":
                case "-":
                case "*":
                case "/":
                case "%":
                    // 算数运算要求两侧都是数字
                    ValidateArithmeticOperands(leftType, rightType, node);
                    return TypeInfo.Number;

                case ">":
                case "<":
                case ">=":
                case "<=":
                    // 关系运算要求两侧都是数字
                    ValidateArithmeticOperands(leftType, rightType, node);
                    return TypeInfo.Boolean;

                case "==":
                case "!=":
                    // 相等运算允许任意类型，但建议类型一致
                    if (leftType.Kind != rightType.Kind && leftType.Kind != TypeKind.Any && rightType.Kind != TypeKind.Any)
                    {
                        _report.AddWarning("SEM051", $"比较不同类型 {leftType} 和 {rightType}", node.Line, node.Column);
                    }
                    return TypeInfo.Boolean;

                case "&&":
                case "||":
                case "xor":
                    // 逻辑运算要求两侧都是布尔
                    ValidateBooleanOperands(leftType, rightType, node);
                    return TypeInfo.Boolean;

                default:
                    _report.AddError("SEM050", $"未知的二元运算符: {node.Operator}", node.Line, node.Column);
                    return TypeInfo.Error;
            }
        }

        /// <summary>
        /// 验证条件表达式
        /// </summary>
        private TypeInfo ValidateConditionExpression(ExpressionNode expr, ScopeManager scopeManager)
        {
            var condType = ValidateExpression(expr, scopeManager);

            if (condType.Kind == TypeKind.Boolean)
            {
                return condType;
            }
            else if (condType.Kind == TypeKind.Number)
            {
                // 数值当布尔使用
                var severity = _options.NumberAsBooleanSeverity;
                if (severity != DiagnosticSeverity.Info)
                {
                    var message = "数值表达式用作条件，将按 0/非0 判断";
                    if (severity == DiagnosticSeverity.Error)
                        _report.AddError("SEM041", message, expr.Line, expr.Column);
                    else
                        _report.AddWarning("SEM041", message, expr.Line, expr.Column);
                }
                return condType;
            }
            else if (condType.Kind == TypeKind.Any)
            {
                AddUnknownTypeWarning("条件表达式", condType, expr);
                return condType;
            }
            else
            {
                _report.AddError("SEM040", $"条件表达式需要布尔类型，但得到 {condType}", expr.Line, expr.Column);
                return TypeInfo.Error;
            }
        }

        /// <summary>
        /// 分析字符串插值
        /// </summary>
        private void AnalyzeStringInterpolation(List<TextSegmentNode> textParts, ScopeManager scopeManager)
        {
            foreach (var part in textParts)
            {
                if (part is InterpolationNode interpolation)
                {
                    var exprType = ValidateExpression(interpolation.Expression, scopeManager);
                    
                    // 检查插值表达式类型
                    if (exprType.Kind != TypeKind.String && exprType.Kind != TypeKind.Number && 
                        exprType.Kind != TypeKind.Boolean && exprType.Kind != TypeKind.Any)
                    {
                        var severity = _options.InvalidStringConcatSeverity;
                        var message = $"字符串插值中的表达式类型 {exprType} 可能无法正确转换为字符串";
                        if (severity == DiagnosticSeverity.Error)
                            _report.AddError("SEM061", message, part.Line, part.Column);
                        else if (severity == DiagnosticSeverity.Warning)
                            _report.AddWarning("SEM061", message, part.Line, part.Column);
                    }
                }
            }
        }

        /// <summary>
        /// 验证算数运算操作数
        /// </summary>
        private void ValidateArithmeticOperands(TypeInfo leftType, TypeInfo rightType, BinaryOpNode node)
        {
            if (leftType.Kind != TypeKind.Number && leftType.Kind != TypeKind.Any)
            {
                _report.AddError("SEM050", $"算数运算符 '{node.Operator}' 的左操作数需要数字类型，但得到 {leftType}", 
                    node.Line, node.Column);
            }
            if (rightType.Kind != TypeKind.Number && rightType.Kind != TypeKind.Any)
            {
                _report.AddError("SEM050", $"算数运算符 '{node.Operator}' 的右操作数需要数字类型，但得到 {rightType}", 
                    node.Line, node.Column);
            }

            // 检查除零
            if ((node.Operator == "/" || node.Operator == "%") && node.Right is NumberNode numberNode)
            {
                if (Math.Abs(numberNode.Value) < double.Epsilon)
                {
                    _report.AddError("SEM051", "除数不能为零", node.Right.Line, node.Right.Column);
                }
            }
        }

        /// <summary>
        /// 验证布尔运算操作数
        /// </summary>
        private void ValidateBooleanOperands(TypeInfo leftType, TypeInfo rightType, BinaryOpNode node)
        {
            if (leftType.Kind != TypeKind.Boolean && leftType.Kind != TypeKind.Any)
            {
                _report.AddError("SEM050", $"逻辑运算符 '{node.Operator}' 的左操作数需要布尔类型，但得到 {leftType}", 
                    node.Line, node.Column);
            }
            if (rightType.Kind != TypeKind.Boolean && rightType.Kind != TypeKind.Any)
            {
                _report.AddError("SEM050", $"逻辑运算符 '{node.Operator}' 的右操作数需要布尔类型，但得到 {rightType}", 
                    node.Line, node.Column);
            }
        }

        /// <summary>
        /// 添加未知类型警告
        /// </summary>
        private void AddUnknownTypeWarning(string operation, TypeInfo type, ASTNode node)
        {
            var severity = _options.UnknownTypeOperationSeverity;
            var message = $"{operation}涉及未知类型 {type}，可能导致运行时错误";
            if (severity == DiagnosticSeverity.Error)
                _report.AddError("SEM070", message, node.Line, node.Column);
            else if (severity == DiagnosticSeverity.Warning)
                _report.AddWarning("SEM070", message, node.Line, node.Column);
        }

        /// <summary>
        /// 获取当前节点名（从 AST 节点上下文推断）
        /// </summary>
        private string GetCurrentNodeName(ASTNode node)
        {
            // 暂时返回空，需要在分析时维护当前节点信息
            return null;
        }

        /// <summary>
        /// 验证一元运算
        /// </summary>
        private TypeInfo ValidateUnaryOperation(UnaryOpNode node, ISymbolTable symbolTable)
        {
            var operandType = ValidateExpressionInternal(node.Operand, symbolTable);

            switch (node.Operator)
            {
                case "-":
                    if (operandType.Kind != TypeKind.Number && operandType.Kind != TypeKind.Any)
                    {
                        _report.AddError("SEM050", $"一元负号运算符需要数字类型，但得到 {operandType}", node.Line, node.Column);
                        return TypeInfo.Error;
                    }
                    return TypeInfo.Number;

                case "not":
                    if (operandType.Kind != TypeKind.Boolean && operandType.Kind != TypeKind.Any)
                    {
                        _report.AddError("SEM050", $"逻辑非运算符需要布尔类型，但得到 {operandType}", node.Line, node.Column);
                        return TypeInfo.Error;
                    }
                    return TypeInfo.Boolean;

                default:
                    _report.AddError("SEM050", $"未知的一元运算符: {node.Operator}", node.Line, node.Column);
                    return TypeInfo.Error;
            }
        }

        /// <summary>
        /// 验证函数调用表达式
        /// </summary>
        private TypeInfo ValidateCallExpression(CallExpressionNode node, ISymbolTable symbolTable)
        {
            var calleeType = ValidateExpressionInternal(node.Callee, symbolTable);

            // 验证参数
            var argumentTypes = new List<TypeInfo>();
            foreach (var arg in node.Arguments)
            {
                var argType = ValidateExpressionInternal(arg, symbolTable);
                argumentTypes.Add(argType);
            }

            // 检查调用方式
            if (calleeType.Kind == TypeKind.Function)
            {
                // 函数值调用
                return ValidateFunctionValueCall(node, argumentTypes);
            }
            else if (node.Callee is IdentifierNode identifierNode)
            {
                // 通过标识符调用 - 检查函数表中是否存在
                return ValidateNamedFunctionCall(identifierNode.Name, node, argumentTypes, symbolTable);
            }
            else if (node.Callee is MemberAccessNode memberAccessNode)
            {
                // 成员函数调用 - 新增支持
                return ValidateMemberFunctionCall(memberAccessNode, node, argumentTypes, symbolTable);
            }
            else if (calleeType.Kind == TypeKind.Object && calleeType.ClrType != null && typeof(Delegate).IsAssignableFrom(calleeType.ClrType))
            {
                // Delegate 对象调用
                return ValidateDelegateCall(node, calleeType, argumentTypes);
            }
            else
            {
                // 使用统一错误码
                _report.AddError(ErrorCode.CALLABLE_NOT_SUPPORTED.ToString(), 
                    $"无法调用类型 {calleeType} 的值", 
                    node.Line, node.Column, 
                    "确保对象是函数、委托或具有 Invoke 方法的类型");
                return TypeInfo.Error;
            }
        }

        /// <summary>
        /// 验证函数值调用
        /// </summary>
        private TypeInfo ValidateFunctionValueCall(CallExpressionNode node, List<TypeInfo> argumentTypes)
        {
            // 函数值调用的类型检查相对宽松，主要在运行时检查
            // 这里可以进行基本的参数数量检查（如果有签名信息的话）
            
            if (_options.EnableSignatureInference)
            {
                // TODO: 如果函数值携带签名信息，可以进行更严格的检查
            }

            return TypeInfo.Any; // 函数值调用返回类型默认为 Any
        }

        /// <summary>
        /// 验证命名函数调用
        /// </summary>
        private TypeInfo ValidateNamedFunctionCall(string functionName, CallExpressionNode node, List<TypeInfo> argumentTypes, ISymbolTable symbolTable)
        {
            var functionInfo = symbolTable.GetFunctionInfo(functionName);
            if (functionInfo == null)
            {
                // 使用ExceptionFactory的逻辑来获取建议
                var suggestion = GetSimilarFunctionName(functionName, symbolTable);
                
                string suggestionText = null;
                if (suggestion != null)
                {
                    suggestionText = $"你是否想要: {suggestion}";
                }
                
                _report.AddError(ErrorCode.SA_FUNC_NOT_FOUND.ToString(), 
                    $"语义分析：未找到函数 '{functionName}'", 
                    node.Line, node.Column, 
                    suggestionText);
                return TypeInfo.Error;
            }

            // 检查大小写一致性
            if (!string.Equals(functionInfo.Name, functionName, StringComparison.Ordinal) && _options.CaseInconsistencyAsWarning)
            {
                _report.AddWarning("SEM012", $"函数名 '{functionName}' 与实际定义 '{functionInfo.Name}' 大小写不一致", 
                    node.Line, node.Column, $"使用 '{functionInfo.Name}'");
            }

            // 检查参数数量
            ValidateFunctionArguments(functionInfo, node.Arguments, node);
            
            // 检查参数类型（如果有详细类型信息）
            ValidateArgumentTypes(functionInfo, argumentTypes, node);

            return functionInfo.ReturnType;
        }

        /// <summary>
        /// 验证成员函数调用
        /// </summary>
        private TypeInfo ValidateMemberFunctionCall(MemberAccessNode memberAccess, CallExpressionNode node, List<TypeInfo> argumentTypes, ISymbolTable symbolTable)
        {
            // 首先检查是否是对象名.方法名格式的已注册函数
            if (memberAccess.Target is IdentifierNode targetIdentifier)
            {
                string memberFunctionKey = $"{targetIdentifier.Name}.{memberAccess.MemberName}";
                var functionInfo = symbolTable.GetFunctionInfo(memberFunctionKey);
                
                if (functionInfo != null)
                {
                    // 找到已注册的成员函数，进行常规函数验证
                    ValidateFunctionArguments(functionInfo, node.Arguments, node);
                    ValidateArgumentTypes(functionInfo, argumentTypes, node);
                    return functionInfo.ReturnType;
                }
            }

            // 如果不是已注册的成员函数，尝试通过成员访问类型推断
            var targetType = ValidateExpressionInternal(memberAccess.Target, symbolTable);

            if (targetType.Kind == TypeKind.Object && targetType.ClrType != null)
            {
                var memberInfo = GetMemberInfo(targetType.ClrType, memberAccess.MemberName);
                
                if (memberInfo == null)
                {
                    var suggestion = GetSimilarMemberName(targetType.ClrType, memberAccess.MemberName);
                    string suggestionText = suggestion != null ? $"你是否想要: {suggestion}" : null;
                    
                    _report.AddError(ErrorCode.SA_MEMBER_UNKNOWN.ToString(),
                        $"类型 {targetType.ClrType.Name} 不包含成员 '{memberAccess.MemberName}'",
                        node.Line, node.Column,
                        suggestionText);
                    return TypeInfo.Error;
                }

                // 检查成员是否可调用
                if (memberInfo.Type == MemberAccessor.AccessorType.Method)
                {
                    // 方法调用 - 可以进行基本的参数数量检查
                    if (memberInfo.Method != null)
                    {
                        var parameters = memberInfo.Method.GetParameters();
                        int expectedArgCount = parameters.Length;
                        int actualArgCount = argumentTypes.Count;
                        int minRequiredArgs = parameters.Count(p => !p.HasDefaultValue);

                        if (actualArgCount < minRequiredArgs)
                        {
                            _report.AddError(ErrorCode.ARG_MISMATCH.ToString(),
                                $"参数数量不匹配：期望至少 {minRequiredArgs} 个，实际 {actualArgCount} 个",
                                node.Line, node.Column,
                                "检查函数调用的参数数量是否正确");
                            return TypeInfo.Error;
                        }
                        
                        // 检查是否有 params 参数
                        bool hasParamsParameter = parameters.Length > 0 && 
                            parameters[parameters.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;
                        
                        if (!hasParamsParameter && actualArgCount > expectedArgCount)
                        {
                            _report.AddError(ErrorCode.ARG_MISMATCH.ToString(),
                                $"参数数量不匹配：期望最多 {expectedArgCount} 个，实际 {actualArgCount} 个",
                                node.Line, node.Column,
                                "检查函数调用的参数数量是否正确");
                            return TypeInfo.Error;
                        }

                        // 返回方法的返回类型
                        return TypeInference.FromClrType(memberInfo.Method.ReturnType);
                    }
                }
                else if (memberInfo.Type == MemberAccessor.AccessorType.Property)
                {
                    // 属性访问，检查类型是否可调用
                    var propertyType = GetMemberType(memberInfo);
                    var propertyTypeInfo = TypeInference.FromClrType(propertyType);
                    
                    if (propertyTypeInfo.Kind == TypeKind.Function || 
                        (propertyTypeInfo.Kind == TypeKind.Object && typeof(Delegate).IsAssignableFrom(propertyType)))
                    {
                        // 可调用的属性，发出未验证警告
                        _report.AddWarning(ErrorCode.SA_CALL_UNVERIFIED.ToString(),
                            $"成员 '{memberAccess.MemberName}' 可能可调用但无法确认参数签名",
                            node.Line, node.Column,
                            "运行时验证");
                        return TypeInfo.Any; // 无法确定返回类型
                    }
                    else
                    {
                        _report.AddError(ErrorCode.CALLABLE_NOT_SUPPORTED.ToString(),
                            $"属性 '{memberAccess.MemberName}' 类型 '{propertyTypeInfo}' 不支持调用操作",
                            node.Line, node.Column,
                            "确保成员是方法、函数或委托类型");
                        return TypeInfo.Error;
                    }
                }
                else if (memberInfo.Type == MemberAccessor.AccessorType.Field)
                {
                    // 字段访问，检查类型是否可调用
                    var fieldType = GetMemberType(memberInfo);
                    var fieldTypeInfo = TypeInference.FromClrType(fieldType);
                    
                    if (fieldTypeInfo.Kind == TypeKind.Function || 
                        (fieldTypeInfo.Kind == TypeKind.Object && typeof(Delegate).IsAssignableFrom(fieldType)))
                    {
                        // 可调用的字段，发出未验证警告
                        _report.AddWarning(ErrorCode.SA_CALL_UNVERIFIED.ToString(),
                            $"字段 '{memberAccess.MemberName}' 可能可调用但无法确认参数签名",
                            node.Line, node.Column,
                            "运行时验证");
                        return TypeInfo.Any; // 无法确定返回类型
                    }
                    else
                    {
                        _report.AddError(ErrorCode.CALLABLE_NOT_SUPPORTED.ToString(),
                            $"字段 '{memberAccess.MemberName}' 类型 '{fieldTypeInfo}' 不支持调用操作",
                            node.Line, node.Column,
                            "确保成员是方法、函数或委托类型");
                        return TypeInfo.Error;
                    }
                }
            }
            else if (targetType.Kind == TypeKind.Any)
            {
                // 未知目标类型，发出未验证警告
                _report.AddWarning(ErrorCode.SA_CALL_UNVERIFIED.ToString(),
                    $"成员函数调用未验证：目标类型未知",
                    node.Line, node.Column,
                    "运行时验证");
                return TypeInfo.Any;
            }
            else
            {
                _report.AddError(ErrorCode.SA_MEMBER_UNKNOWN.ToString(),
                    $"无法访问类型 {targetType} 的成员",
                    node.Line, node.Column);
                return TypeInfo.Error;
            }

            // 默认情况：无法确定但不阻断执行
            _report.AddWarning(ErrorCode.SA_CALL_UNVERIFIED.ToString(),
                $"成员函数调用未验证",
                node.Line, node.Column,
                "运行时验证");
            return TypeInfo.Any;
        }

        /// <summary>
        /// 验证委托调用
        /// </summary>
        private TypeInfo ValidateDelegateCall(CallExpressionNode node, TypeInfo delegateType, List<TypeInfo> argumentTypes)
        {
            // 对于 Delegate 类型，我们可以通过反射获取其方法签名
            if (delegateType.ClrType != null)
            {
                var invokeMethod = delegateType.ClrType.GetMethod("Invoke");
                if (invokeMethod != null)
                {
                    var parameters = invokeMethod.GetParameters();
                    
                    // 检查参数数量
                    if (argumentTypes.Count != parameters.Length)
                    {
                        _report.AddError("SEM013", $"委托调用需要 {parameters.Length} 个参数，但提供了 {argumentTypes.Count} 个", 
                            node.Line, node.Column);
                        return TypeInfo.Error;
                    }

                    // 检查参数类型
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var expectedType = TypeInference.FromClrType(parameters[i].ParameterType);
                        var actualType = argumentTypes[i];
                        
                        if (!IsTypeCompatible(actualType, expectedType))
                        {
                            _report.AddError("SEM014", $"第 {i + 1} 个参数类型不匹配：期望 {expectedType}，但得到 {actualType}", 
                                node.Arguments[i].Line, node.Arguments[i].Column);
                        }
                    }

                    // 返回类型
                    return TypeInference.FromClrType(invokeMethod.ReturnType);
                }
            }

            return TypeInfo.Any;
        }

        /// <summary>
        /// 验证成员访问
        /// </summary>
        private TypeInfo ValidateMemberAccess(MemberAccessNode node, ISymbolTable symbolTable)
        {
            var targetType = ValidateExpressionInternal(node.Target, symbolTable);

            if (targetType.Kind == TypeKind.Any)
            {
                AddUnknownTypeWarning("成员访问", targetType, node);
                return TypeInfo.Any;
            }

            if (targetType.Kind == TypeKind.Object && targetType.ClrType != null)
            {
                var memberInfo = GetMemberInfo(targetType.ClrType, node.MemberName);
                if (memberInfo == null)
                {
                    var suggestion = GetSimilarMemberName(targetType.ClrType, node.MemberName);
                    var suggestionText = suggestion != null ? $"，你是否想要访问 '{suggestion}'?" : "";
                    _report.AddError("SEM020", $"类型 {targetType.ClrType.Name} 不包含成员 '{node.MemberName}'{suggestionText}", 
                        node.Line, node.Column, suggestion);
                    return TypeInfo.Error;
                }

                // 检查大小写一致性（如果 Helper 返回的访问器有名称信息）
                if (memberInfo != null && !string.IsNullOrEmpty(memberInfo.Name) && 
                    !string.Equals(memberInfo.Name, node.MemberName, StringComparison.Ordinal) && 
                    _options.CaseInconsistencyAsWarning)
                {
                    _report.AddWarning("SEM021", $"成员名 '{node.MemberName}' 与实际定义 '{memberInfo.Name}' 大小写不一致", 
                        node.Line, node.Column, $"使用 '{memberInfo.Name}'");
                }

                // 根据成员类型返回相应的类型信息
                switch (memberInfo.Type)
                {
                    case MemberAccessor.AccessorType.Method:
                        return TypeInfo.Function;
                    case MemberAccessor.AccessorType.Property:
                    case MemberAccessor.AccessorType.Field:
                        return TypeInference.FromClrType(GetMemberType(memberInfo));
                    default:
                        return TypeInfo.Any;
                }
            }

            _report.AddError("SEM020", $"无法访问类型 {targetType} 的成员", node.Line, node.Column);
            return TypeInfo.Error;
        }

        /// <summary>
        /// 验证索引访问
        /// </summary>
        private TypeInfo ValidateIndexAccess(IndexAccessNode node, ISymbolTable symbolTable)
        {
            var targetType = ValidateExpressionInternal(node.Target, symbolTable);
            var indexType = ValidateExpressionInternal(node.Index, symbolTable);

            if (targetType.Kind == TypeKind.Any)
            {
                AddUnknownTypeWarning("索引访问", targetType, node);
                return TypeInfo.Any;
            }

            switch (targetType.Kind)
            {
                case TypeKind.String:
                    if (indexType.Kind != TypeKind.Number && indexType.Kind != TypeKind.Any)
                    {
                        _report.AddError("SEM030", $"字符串索引需要数字类型，但得到 {indexType}", node.Line, node.Column);
                        return TypeInfo.Error;
                    }
                    return TypeInfo.String;

                case TypeKind.Array:
                    if (indexType.Kind != TypeKind.Number && indexType.Kind != TypeKind.Any)
                    {
                        _report.AddError("SEM030", $"数组索引需要数字类型，但得到 {indexType}", node.Line, node.Column);
                        return TypeInfo.Error;
                    }
                    return targetType.ElementType ?? TypeInfo.Any;

                case TypeKind.Dictionary:
                    // 字典键类型检查
                    if (targetType.KeyType != null && !IsTypeCompatible(indexType, targetType.KeyType))
                    {
                        _report.AddError("SEM030", $"字典键类型不匹配：期望 {targetType.KeyType}，但得到 {indexType}", 
                            node.Index.Line, node.Index.Column);
                        return TypeInfo.Error;
                    }
                    return targetType.ValueType ?? TypeInfo.Any;

                default:
                    _report.AddError("SEM030", $"类型 {targetType} 不支持索引访问", node.Line, node.Column);
                    return TypeInfo.Error;
            }
        }

        /// <summary>
        /// 验证变量
        /// </summary>
        private TypeInfo ValidateVariable(VariableNode node, ISymbolTable symbolTable)
        {
            var variableType = symbolTable.GetVariableType(node.Name);
            
            if (variableType.Kind == TypeKind.Error)
            {
                var suggestion = GetSimilarVariableName(node.Name, symbolTable);
                var suggestionText = suggestion != null ? $"，你是否想要使用 '{suggestion}'?" : "";
                _report.AddError("SEM001", $"未定义的变量 '{node.Name}'{suggestionText}", 
                    node.Line, node.Column, suggestion);
            }

            return variableType;
        }

        /// <summary>
        /// 验证标识符
        /// </summary>
        private TypeInfo ValidateIdentifier(IdentifierNode node, ISymbolTable symbolTable)
        {
            var identifierType = symbolTable.GetIdentifierType(node.Name);
            
            if (identifierType.Kind == TypeKind.Error)
            {
                var suggestion = GetSimilarIdentifierName(node.Name, symbolTable);
                var suggestionText = suggestion != null ? $"，你是否想要使用 '{suggestion}'?" : "";
                _report.AddError("SEM001", $"未定义的标识符 '{node.Name}'{suggestionText}", 
                    node.Line, node.Column, suggestion);
            }

            return identifierType;
        }

        /// <summary>
        /// 验证函数参数
        /// </summary>
        private void ValidateFunctionArguments(FunctionInfo functionInfo, List<ExpressionNode> arguments, CallExpressionNode node)
        {
            var argCount = arguments.Count;
            
            if (argCount < functionInfo.MinParameters)
            {
                _report.AddError("SEM013", $"函数 '{functionInfo.Name}' 需要至少 {functionInfo.MinParameters} 个参数，但提供了 {argCount} 个", 
                    node.Line, node.Column);
            }
            else if (argCount > functionInfo.MaxParameters)
            {
                _report.AddError("SEM013", $"函数 '{functionInfo.Name}' 最多接受 {functionInfo.MaxParameters} 个参数，但提供了 {argCount} 个", 
                    node.Line, node.Column);
            }
        }

        /// <summary>
        /// 验证参数类型
        /// </summary>
        private void ValidateArgumentTypes(FunctionInfo functionInfo, List<TypeInfo> argumentTypes, CallExpressionNode node)
        {
            if (!_options.EnableSignatureInference || functionInfo.ParameterTypes.Count == 0)
                return;

            for (int i = 0; i < Math.Min(argumentTypes.Count, functionInfo.ParameterTypes.Count); i++)
            {
                var expectedType = functionInfo.ParameterTypes[i];
                var actualType = argumentTypes[i];
                
                if (!IsTypeCompatible(actualType, expectedType))
                {
                    _report.AddError("SEM014", $"函数 '{functionInfo.Name}' 的第 {i + 1} 个参数类型不匹配：期望 {expectedType}，但得到 {actualType}", 
                        node.Arguments[i].Line, node.Arguments[i].Column);
                }
            }
        }

        /// <summary>
        /// 检查类型兼容性
        /// </summary>
        private bool IsTypeCompatible(TypeInfo actualType, TypeInfo expectedType)
        {
            // Any 类型与任何类型兼容
            if (actualType.Kind == TypeKind.Any || expectedType.Kind == TypeKind.Any)
                return true;

            // 相同类型兼容
            if (actualType.Kind == expectedType.Kind)
                return true;

            // 特殊兼容性规则
            switch (expectedType.Kind)
            {
                case TypeKind.Number:
                    // 数字类型之间可以转换
                    return actualType.Kind == TypeKind.Number;
                    
                case TypeKind.String:
                    // 基本类型可以转换为字符串
                    return actualType.Kind == TypeKind.Number || actualType.Kind == TypeKind.Boolean;
                    
                case TypeKind.Boolean:
                    // 数字可以转换为布尔（但会产生警告）
                    return actualType.Kind == TypeKind.Number;
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取成员信息（使用Helper的缓存机制）
        /// </summary>
        private MemberAccessor GetMemberInfo(Type type, string memberName)
        {
            // 直接使用Helper的缓存成员访问器
            return Helper.GetMemberAccessor(type, memberName);
        }
        
        /// <summary>
        /// 获取成员信息的备用实现（如果Helper方法不可用）
        /// </summary>
        private MemberInfo GetMemberInfoFallback(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;
            
            // 查找属性
            var property = type.GetProperty(memberName, flags);
            if (property != null)
                return new MemberInfo(property.PropertyType, MemberKind.Property, property.CanWrite);

            // 查找字段
            var field = type.GetField(memberName, flags);
            if (field != null)
                return new MemberInfo(field.FieldType, MemberKind.Field, !field.IsInitOnly);

            // 查找方法
            var methods = type.GetMethods(flags).Where(m => string.Equals(m.Name, memberName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (methods.Length > 0)
                return new MemberInfo(typeof(Delegate), MemberKind.Method, false);

            return null;
        }

        /// <summary>
        /// 从 MemberAccessor 获取成员类型（通过反射获取准确类型）
        /// </summary>
        private Type GetMemberType(MemberAccessor accessor)
        {
            switch (accessor.Type)
            {
                case MemberAccessor.AccessorType.Method:
                    return typeof(Delegate); // 方法返回函数类型

                case MemberAccessor.AccessorType.Property:
                    // 通过反射获取属性的准确类型
                    if (accessor.Getter != null)
                    {
                        try
                        {
                            // 尝试通过Method获取返回类型
                            var getterMethod = accessor.Getter.Method;
                            if (getterMethod != null)
                            {
                                return getterMethod.ReturnType;
                            }
                        }
                        catch
                        {
                            // 如果获取失败，返回 object 类型
                        }
                    }
                    return typeof(object);

                case MemberAccessor.AccessorType.Field:
                    // 对于字段，也尝试通过 getter 获取类型信息
                    if (accessor.Getter != null)
                    {
                        try
                        {
                            var getterMethod = accessor.Getter.Method;
                            if (getterMethod != null)
                            {
                                return getterMethod.ReturnType;
                            }
                        }
                        catch
                        {
                            // 如果获取失败，返回 object 类型
                        }
                    }
                    return typeof(object);

                default:
                    return typeof(object);
            }
        }

        /// <summary>
        /// 获取相似函数名建议
        /// </summary>
        private string GetSimilarFunctionName(string name, ISymbolTable symbolTable)
        {
            if (symbolTable is GlobalSymbolTable globalTable)
            {
                var functionNames = globalTable.GetLocalFunctionNames();
                return GetSimilarName(name, functionNames);
            }
            return null;
        }

        /// <summary>
        /// 获取相似成员名建议
        /// </summary>
        private string GetSimilarMemberName(Type type, string memberName)
        {
            if (type == null) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            var memberNames = new List<string>();

            // 收集所有成员名
            memberNames.AddRange(type.GetProperties(flags).Select(p => p.Name));
            memberNames.AddRange(type.GetFields(flags).Select(f => f.Name));
            memberNames.AddRange(type.GetMethods(flags).Where(m => !m.IsSpecialName).Select(m => m.Name));

            return GetSimilarName(memberName, memberNames);
        }

        /// <summary>
        /// 获取相似变量名建议
        /// </summary>
        private string GetSimilarVariableName(string name, ISymbolTable symbolTable)
        {
            if (symbolTable is SymbolTable localTable)
            {
                var variableNames = localTable.GetLocalVariableNames();
                return GetSimilarName(name, variableNames);
            }
            return null;
        }

        /// <summary>
        /// 获取相似标识符名建议
        /// </summary>
        private string GetSimilarIdentifierName(string name, ISymbolTable symbolTable)
        {
            // 先尝试变量名，再尝试函数名
            return GetSimilarVariableName(name, symbolTable) ?? GetSimilarFunctionName(name, symbolTable);
        }

        /// <summary>
        /// 通用相似名称查找方法
        /// </summary>
        private string GetSimilarName(string target, IEnumerable<string> candidates)
        {
            if (string.IsNullOrEmpty(target) || candidates == null)
                return null;

            var similarities = candidates
                .Select(c => new { Name = c, Distance = LevenshteinDistance(target.ToLower(), c.ToLower()) })
                .Where(s => s.Distance <= Math.Max(2, target.Length / 2)) // 限制编辑距离
                .OrderBy(s => s.Distance)
                .ThenBy(s => s.Name)
                .Take(1)
                .Select(s => s.Name)
                .FirstOrDefault();

            return similarities;
        }

        /// <summary>
        /// 计算编辑距离
        /// </summary>
        private static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return string.IsNullOrEmpty(target) ? 0 : target.Length;

            if (string.IsNullOrEmpty(target))
                return source.Length;

            var matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source.Length, target.Length];
        }
    }
}
