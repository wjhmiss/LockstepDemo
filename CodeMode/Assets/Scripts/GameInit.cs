using System;
using CommandLine;
using UnityEngine;

namespace ET.Client
{
    /// <summary>
    /// Unity 入口 MonoBehaviour，负责启动 ET 框架。
    /// 该脚本位于 Assets/Scripts/ 下，默认属于 Assembly-CSharp.dll。
    ///
    /// 注意：类名为 GameInit（不使用 Init）以避免与 cn.etetet.loader 包自带的
    /// ET.Client.Init 类冲突。本类对资源系统失败做了容错处理，适合学习项目。
    ///
    /// 启动流程（与 ET 原版 Loader 方案一致）：
    /// 1. 加载 GlobalConfig 资源，解析命令行参数到 Options
    /// 2. 注册全局单例：Logger / TimeInfo / FiberManager
    /// 3. 加载资源包 DefaultPackage（ResourcesComponent，失败不阻塞）
    /// 4. 启动 CodeLoader，由 CodeLoader 反射调用 ET.Entry.Start()（业务入口在 Hotfix 层）
    /// 5. Update/LateUpdate 驱动 FiberManager
    /// </summary>
    public class GameInit : MonoBehaviour
    {
        private void Start()
        {
            this.StartAsync().Coroutine();
        }

        private async ETTask StartAsync()
        {
            DontDestroyOnLoad(gameObject);

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Log.Error(e.ExceptionObject.ToString());
            };

            // 1. 加载全局配置
            GlobalConfig globalConfig = Resources.Load<GlobalConfig>("GlobalConfig");
            if (globalConfig == null)
            {
                Debug.LogError("[GameInit] GlobalConfig 资源未找到！请在 Assets/Resources/ 创建 GlobalConfig.asset");
                return;
            }

            // 2. 命令行参数（使用 GlobalConfig.SceneName）
            string[] args = { $"--SceneName={globalConfig.SceneName}" };
            Parser.Default.ParseArguments<Options>(args)
                .WithNotParsed(error => throw new Exception($"命令行格式错误! {error}"))
                .WithParsed((o) => World.Instance.AddSingleton(o));

            // 编辑器模式下如果开启了 ENABLE_VIEW 使用单线程，WEBGL 模式也使用单线程
#if (ENABLE_VIEW && UNITY_EDITOR) || UNITY_WEBGL
            Options.Instance.SingleThread = 1;
#endif

            // 3. 注册全局日志（使用 Unity Logger）
            World.Instance.AddSingleton<Logger>().Log = new UnityLogger("None");
            ETTask.ExceptionHandler += Log.Error;

            // 4. 注册时间信息和 Fiber 管理器
            World.Instance.AddSingleton<TimeInfo>();
            World.Instance.AddSingleton<FiberManager>();

            try
            {
                // 5. 加载资源包（编辑器模式下会自动创建 DefaultPackage）
                // 学习项目可能未配置 YooAsset 资源服务器，失败不阻塞启动
                await World.Instance.AddSingleton<ResourcesComponent>().CreatePackageAsync("DefaultPackage", true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameInit] ResourcesComponent CreatePackageAsync failed (non-blocking): {e.Message}");
            }

            // 6. 启动 CodeLoader，会反射调用 ET.Entry.Start()
            World.Instance.AddSingleton<CodeLoader>().Start().Coroutine();
        }

        private void Update()
        {
            FiberManager.Instance?.Update();
        }

        private void LateUpdate()
        {
            FiberManager.Instance?.LateUpdate();
        }

        private void OnApplicationQuit()
        {
            World.Instance?.Dispose();
        }
    }
}
