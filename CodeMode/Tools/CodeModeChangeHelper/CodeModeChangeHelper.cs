using System;
using System.Collections.Generic;
using System.IO;

namespace ET
{
    /// <summary>
    /// 简化版 CodeModeChangeHelper
    /// 模拟 ET 框架 Packages/com.etetet.init/DotNet~/CodeModeChangeHelper.cs 的行为
    /// 用于演示 All-in-One + CodeMode 架构的切换逻辑
    ///
    /// 用法：
    ///   dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=Client
    ///   dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=Server
    ///   dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=ClientServer
    /// </summary>
    public static class CodeModeChangeHelper
    {
        // ModelDir → asmdef 名称的映射
        private static readonly Dictionary<string, string> ModelDirToAsmdef = new()
        {
            { "Model", "ET.Model" },
            { "Hotfix", "ET.Hotfix" },
            { "ModelView", "ET.ModelView" },
            { "HotfixView", "ET.HotfixView" },
        };

        // CodeMode → 应生成 asmref 的 ServerDir 列表
        private static readonly Dictionary<string, string[]> CodeModeRules = new()
        {
            { "Client", new[] { "Share", "Client" } },
            { "Server", new[] { "Share", "Server" } },
            { "ClientServer", new[] { "Share", "Client", "Server" } },
        };

        public static int Main(string[] args)
        {
            // 解析参数
            string codeMode = null;
            foreach (var arg in args)
            {
                if (arg.StartsWith("--CodeMode="))
                {
                    codeMode = arg.Substring("--CodeMode=".Length);
                }
            }

            if (string.IsNullOrEmpty(codeMode) || !CodeModeRules.ContainsKey(codeMode))
            {
                Console.Error.WriteLine($"[ERROR] Invalid CodeMode. Usage: --CodeMode=<Client|Server|ClientServer>");
                Console.Error.WriteLine($"  Received: {codeMode ?? "(null)"}");
                return 1;
            }

            Console.WriteLine("========================================");
            Console.WriteLine($"  Switching to CodeMode: {codeMode}");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // 项目根目录：CodeModeChangeHelper.csproj 的上 3 级
            string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            // 调整：实际从环境当前目录推断（让用户在任何目录运行都准确）
            // 我们以当前工作目录为基础，寻找 Packages/cn.codemode.helloworld
            // 但更稳妥的做法：从 csproj 位置推断
            // 由于 dotnet run 的工作目录默认是项目目录，我们采用以下策略：
            string packageRoot = FindPackageRoot();

            if (packageRoot == null)
            {
                Console.Error.WriteLine("[ERROR] Cannot find 'Packages/cn.codemode.helloworld' directory.");
                Console.Error.WriteLine("        Please run this tool from the CodeMode project root.");
                return 2;
            }

            Console.WriteLine($"Project Root: {Path.GetDirectoryName(packageRoot)}");
            Console.WriteLine($"Package Root: {packageRoot}");
            Console.WriteLine();

            // Step 1: 删除所有现有的 AssemblyReference.asmref
            Console.WriteLine("[Step 1] Deleting all existing AssemblyReference.asmref files...");
            int deletedCount = 0;
            foreach (var asmrefFile in Directory.EnumerateFiles(packageRoot, "AssemblyReference.asmref", SearchOption.AllDirectories))
            {
                Console.WriteLine($"  [-] Deleted: {Path.GetRelativePath(packageRoot, asmrefFile)}");
                File.Delete(asmrefFile);
                deletedCount++;
            }
            Console.WriteLine($"  Total deleted: {deletedCount}");
            Console.WriteLine();

            // Step 2: 根据 CodeMode 生成新的 asmref 文件
            Console.WriteLine($"[Step 2] Creating new AssemblyReference.asmref files for CodeMode={codeMode}...");
            string[] allowedServerDirs = CodeModeRules[codeMode];
            int createdCount = 0;

            foreach (var kv in ModelDirToAsmdef)
            {
                string modelDir = kv.Key;
                string asmdefName = kv.Value;

                foreach (string serverDir in allowedServerDirs)
                {
                    string targetDir = Path.Combine(packageRoot, modelDir, serverDir);
                    if (!Directory.Exists(targetDir))
                    {
                        Console.WriteLine($"  [skip] Directory not exists: {modelDir}/{serverDir}");
                        continue;
                    }
                    string asmrefPath = Path.Combine(targetDir, "AssemblyReference.asmref");
                    string content = $"{{ \"reference\": \"{asmdefName}\" }}";
                    File.WriteAllText(asmrefPath, content);
                    Console.WriteLine($"  [+] Created: {Path.GetRelativePath(packageRoot, asmrefPath)}");
                    Console.WriteLine($"      Content: {content}");
                    createdCount++;
                }
            }
            Console.WriteLine();
            Console.WriteLine($"  Total created: {createdCount}");
            Console.WriteLine();

            // Step 3: 摘要
            Console.WriteLine("[Step 3] Summary...");
            Console.WriteLine($"  CodeMode: {codeMode}");
            Console.WriteLine($"  Allowed ServerDirs: {string.Join(", ", allowedServerDirs)}");
            Console.WriteLine($"  Deleted asmref: {deletedCount}");
            Console.WriteLine($"  Created asmref: {createdCount}");
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("  CodeMode switch completed!");
            Console.WriteLine("========================================");

            return 0;
        }

        /// <summary>
        /// 从当前工作目录开始向上查找，定位 Packages/cn.codemode.helloworld/Scripts 目录
        /// </summary>
        private static string FindPackageRoot()
        {
            string current = Directory.GetCurrentDirectory();
            for (int i = 0; i < 10; i++)
            {
                string candidate = Path.Combine(current, "Packages", "cn.codemode.helloworld", "Scripts");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            return null;
        }
    }
}
