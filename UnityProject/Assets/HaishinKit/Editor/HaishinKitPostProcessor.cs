#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace HaishinKit.Editor
{
    /// <summary>
    /// iOS ビルド後処理
    /// - カメラ/マイク権限の追加
    /// - バックグラウンドモード設定
    /// - Swift ランタイム設定
    /// </summary>
    public class HaishinKitPostProcessor
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
                return;

            // Info.plist の編集
            ModifyInfoPlist(path);

            // Xcode プロジェクトの編集
            ModifyXcodeProject(path);
        }

        private static void ModifyInfoPlist(string path)
        {
            string plistPath = Path.Combine(path, "Info.plist");
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            // カメラ使用権限
            if (!plist.root.values.ContainsKey("NSCameraUsageDescription"))
            {
                plist.root.SetString("NSCameraUsageDescription", "配信にカメラを使用します");
            }

            // マイク使用権限
            if (!plist.root.values.ContainsKey("NSMicrophoneUsageDescription"))
            {
                plist.root.SetString("NSMicrophoneUsageDescription", "配信にマイクを使用します");
            }

            // バックグラウンドモード（オーディオ）
            PlistElementArray bgModes;
            if (plist.root.values.ContainsKey("UIBackgroundModes"))
            {
                bgModes = plist.root["UIBackgroundModes"].AsArray();
            }
            else
            {
                bgModes = plist.root.CreateArray("UIBackgroundModes");
            }

            // audio モードを追加（重複チェック）
            bool hasAudio = false;
            foreach (var mode in bgModes.values)
            {
                if (mode.AsString() == "audio")
                {
                    hasAudio = true;
                    break;
                }
            }
            if (!hasAudio)
            {
                bgModes.AddString("audio");
            }

            plist.WriteToFile(plistPath);
        }

        private static void ModifyXcodeProject(string path)
        {
            string projPath = PBXProject.GetPBXProjectPath(path);
            PBXProject proj = new PBXProject();
            proj.ReadFromFile(projPath);

            // Unity Main Target
            string mainTargetGuid = proj.GetUnityMainTargetGuid();

            // UnityFramework Target
            string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid();


            // Swift Standard Libraries を常に埋め込む
            proj.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            proj.SetBuildProperty(frameworkTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");

            // Swift バージョン設定
            proj.SetBuildProperty(mainTargetGuid, "SWIFT_VERSION", "5.0");
            proj.SetBuildProperty(frameworkTargetGuid, "SWIFT_VERSION", "5.0");

            // Framework Search Paths に追加
            proj.AddBuildProperty(mainTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(mainTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks/Plugins/iOS/HaishinKitUnity");
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks/Plugins/iOS/HaishinKitUnity");

            // LD_RUNPATH_SEARCH_PATHS に追加（動的フレームワーク用）
            proj.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@loader_path/Frameworks");

            // Enable Modules
            proj.SetBuildProperty(mainTargetGuid, "CLANG_ENABLE_MODULES", "YES");
            proj.SetBuildProperty(frameworkTargetGuid, "CLANG_ENABLE_MODULES", "YES");

            // Bitcode 無効化（HaishinKit は Bitcode 非対応）
            proj.SetBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
            proj.SetBuildProperty(frameworkTargetGuid, "ENABLE_BITCODE", "NO");

            proj.WriteToFile(projPath);
        }
    }
}
#endif
