using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ReunionMovement.Core.Terminal
{
    /// <summary>
    /// 命令信息
    /// </summary>
    public struct CommandInfo
    {
        public Action<CommandArg[]> proc;
        public int max_arg_count;
        public int min_arg_count;
        public string help;
        public string hint;
    }

    /// <summary>
    /// 命令参数
    /// </summary>
    public struct CommandArg
    {
        public string String { get; set; }

        public int Int
        {
            get
            {
                int int_value;

                if (int.TryParse(String, out int_value))
                {
                    return int_value;
                }

                TypeError("int");
                return 0;
            }
        }

        public float Float
        {
            get
            {
                float float_value;

                if (float.TryParse(String, out float_value))
                {
                    return float_value;
                }

                TypeError("float");
                return 0;
            }
        }

        public bool Bool
        {
            get
            {
                if (string.Compare(String, "TRUE", ignoreCase: true) == 0)
                {
                    return true;
                }

                if (string.Compare(String, "FALSE", ignoreCase: true) == 0)
                {
                    return false;
                }

                TypeError("bool");
                return false;
            }
        }

        public override string ToString()
        {
            return String;
        }

        void TypeError(string expected_type)
        {
            string err = string.Format("类型错误{0}, 应为<{1}>", expected_type, String);
            Log.Error(err);
        }
    }

    /// <summary>
    /// 终端请求
    /// </summary>
    public class TerminalRequest : MonoBehaviour
    {
        // 命令
        private Dictionary<string, CommandInfo> commands = new Dictionary<string, CommandInfo>();
        // 变量
        private Dictionary<string, CommandArg> variables = new Dictionary<string, CommandArg>();
        // 参数
        List<CommandArg> arguments = new List<CommandArg>();

        public Dictionary<string, CommandInfo> Commands
        {
            get { return commands; }
        }

        public Dictionary<string, CommandArg> Variables
        {
            get { return variables; }
        }

        // 请求
        string request;
        public string Request
        {
            set { request = value; }
            get { return request; }
        }

        /// <summary>
        /// 注册命令 —— 使用源码生成器生成的注册表（编译期扫描，替代运行时全程序集反射）。
        /// 生成器在每个编译目标下基于实际源码生成：条件编译（#if UNITY_EDITOR 等）
        /// 裁剪掉的方法不会出现在生成表中，与旧反射逻辑结果一致。
        /// </summary>
        public void RegisterCommands()
        {
            TerminalCommandRegistry.RegisterAll(this);
        }

        /// <summary>
        /// 解析命令
        /// </summary>
        /// <param name="str"></param>
        public void ParseCommand(string allCommand)
        {
            arguments.Clear();

            // 空输入防护：allCommand 为 null 时 `null != ""` 成立进入循环，
            // ParseArgument 内 TrimStart 会 NRE（终端 UI 传入空输入时触发）
            if (string.IsNullOrEmpty(allCommand))
            {
                return;
            }

            string remaining = allCommand;

            while (remaining != "")
            {
                var argument = ParseArgument(ref remaining);

                if (argument.String != "")
                {
                    if (argument.String[0] == '$')
                    {
                        string variable_name = argument.String.Substring(1).ToUpper();

                        if (variables.ContainsKey(variable_name))
                        {
                            // 替换变量参数（如果已定义）
                            argument = variables[variable_name];
                        }
                    }
                    arguments.Add(argument);
                }
            }

            if (arguments.Count == 0)
            {
                return;
            }

            string command_name = arguments[0].String.ToUpper();
            //从参数中删除命令名
            arguments.RemoveAt(0);

            if (!commands.ContainsKey(command_name))
            {
                ErrorLog("找不到命令：{0}", command_name);
                return;
            }

            ExecuteCommand(command_name, arguments.ToArray());
        }
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="command_name"></param>
        /// <param name="arguments"></param>
        public void ExecuteCommand(string command_name, CommandArg[] arguments)
        {
            var command = commands[command_name];
            int arg_count = arguments.Length;
            string error_message = null;
            int required_arg = 0;

            if (arg_count < command.min_arg_count)
            {
                if (command.min_arg_count == command.max_arg_count)
                {
                    error_message = "exactly";
                }
                else
                {
                    error_message = "at least";
                }
                required_arg = command.min_arg_count;
            }
            else if (command.max_arg_count > -1 && arg_count > command.max_arg_count)
            {
                // Do not check max allowed number of arguments if it is -1
                if (command.min_arg_count == command.max_arg_count)
                {
                    error_message = "exactly";
                }
                else
                {
                    error_message = "at most";
                }
                required_arg = command.max_arg_count;
            }

            if (error_message != null)
            {
                string plural_fix = required_arg == 1 ? "" : "s";

                ErrorLog(
                    "{0} 需要 {1} {2} 参数{3}\n    -> 使用: {4}",
                    command_name,
                    error_message,
                    required_arg,
                    plural_fix,
                    command.hint != null ? command.hint : "--"
                );
                return;
            }

            try
            {
                command.proc(arguments);
            }
            catch (Exception ex)
            {
                // 单命令异常隔离：不得中断输入链路与后续命令（原直接调用，异常会沿解析栈传播）
                ErrorLog("命令 {0} 执行异常: {1}", command_name, ex.Message);
            }
        }

        /// <summary>
        /// 添加命令
        /// </summary>
        /// <param name="name"></param>
        /// <param name="info"></param>
        public void AddCommand(string name, CommandInfo info)
        {
            name = name.ToUpper();

            if (commands.ContainsKey(name))
            {
                ErrorLog("命令[{0}]已定义.", name);
                return;
            }

            commands.Add(name, info);
        }

        /// <summary>
        /// 添加命令
        /// </summary>
        /// <param name="name"></param>
        /// <param name="proc"></param>
        /// <param name="min_args"></param>
        /// <param name="max_args"></param>
        /// <param name="help"></param>
        /// <param name="hint"></param>
        public void AddCommand(string name, Action<CommandArg[]> proc, int min_args = 0, int max_args = -1, string help = "", string hint = null)
        {
            var info = new CommandInfo()
            {
                proc = proc,
                min_arg_count = min_args,
                max_arg_count = max_args,
                help = help,
                hint = hint
            };

            AddCommand(name, info);
        }

        /// <summary>
        /// 设置变量
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetVariable(string name, string value)
        {
            SetVariable(name, new CommandArg() { String = value });
        }

        /// <summary>
        /// 设置变量
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetVariable(string name, CommandArg value)
        {
            name = name.ToUpper();

            if (variables.ContainsKey(name))
            {
                variables[name] = value;
            }
            else
            {
                variables.Add(name, value);
            }
        }

        /// <summary>
        /// 根据名称获得变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public CommandArg GetVariable(string name)
        {
            name = name.ToUpper();

            if (variables.ContainsKey(name))
            {
                return variables[name];
            }

            ErrorLog("没有命名的变量 {0}", name);
            return new CommandArg();
        }

        /// <summary>
        /// 推断命令名称
        /// </summary>
        /// <param name="method_name"></param>
        /// <returns></returns>
        string InferCommandName(string method_name)
        {
            string command_name;
            int index = method_name.IndexOf("COMMAND", StringComparison.CurrentCultureIgnoreCase);

            if (index >= 0)
            {
                // 方法的前缀、后缀为“COMMAND”或包含“COMMAND”。
                command_name = method_name.Remove(index, 7);
            }
            else
            {
                command_name = method_name;
            }

            return command_name;
        }

        /// <summary>
        /// 推断前端命令名称
        /// </summary>
        /// <param name="method_name"></param>
        /// <returns></returns>
        string InferFrontCommandName(string method_name)
        {
            int index = method_name.IndexOf("FRONT", StringComparison.CurrentCultureIgnoreCase);
            return index >= 0 ? method_name.Remove(index, 5) : null;
        }

        /// <summary>
        /// 处理被拒绝的命令
        /// </summary>
        /// <param name="rejected_commands"></param>
        void HandleRejectedCommands(Dictionary<string, CommandInfo> rejected_commands)
        {
            foreach (var command in rejected_commands)
            {
                if (commands.ContainsKey(command.Key))
                {
                    commands[command.Key] = new CommandInfo()
                    {
                        proc = commands[command.Key].proc,
                        min_arg_count = command.Value.min_arg_count,
                        max_arg_count = command.Value.max_arg_count,
                        help = command.Value.help
                    };
                }
                else
                {
                    ErrorLog("{0} 命令前端无法匹配.", command);
                }
            }
        }

        /// <summary>
        /// 来自参数信息的命令
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="help"></param>
        /// <returns></returns>
        CommandInfo CommandFromParamInfo(ParameterInfo[] parameters, string help)
        {
            int optional_args = 0;

            foreach (var param in parameters)
            {
                if (param.IsOptional)
                {
                    optional_args += 1;
                }
            }

            return new CommandInfo()
            {
                proc = null,
                min_arg_count = parameters.Length - optional_args,
                max_arg_count = parameters.Length,
                help = help
            };
        }

        /// <summary>
        /// 将字符串解析成命令参数。
        /// 支持双引号包裹带空格参数（如 cmd "hello world"）；未闭合引号时取整段剩余。
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        CommandArg ParseArgument(ref string s)
        {
            var arg = new CommandArg();

            // 跳过前导空白（同时保证外层 while 循环必然前进，不会在纯空白输入上死循环）
            s = s.TrimStart();
            if (s.Length == 0)
            {
                arg.String = "";
                return arg;
            }

            // 引号包裹：支持带空格参数
            if (s[0] == '"')
            {
                int endQuote = s.IndexOf('"', 1);
                if (endQuote >= 0)
                {
                    arg.String = s.Substring(1, endQuote - 1);
                    s = s.Substring(endQuote + 1);
                    return arg;
                }
                // 未闭合引号：整段剩余作为参数
                arg.String = s.Substring(1);
                s = "";
                return arg;
            }

            int space_index = s.IndexOf(' ');

            if (space_index >= 0)
            {
                arg.String = s.Substring(0, space_index);
                s = s.Substring(space_index + 1); // 剩余部分
            }
            else
            {
                arg.String = s;
                s = "";
            }

            return arg;
        }


        /// <summary>
        /// 日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="message"></param>
        public void ErrorLog(string format, params object[] message)
        {
            string str = string.Format(format, message);
            Log.Error(str);
        }
    }
}