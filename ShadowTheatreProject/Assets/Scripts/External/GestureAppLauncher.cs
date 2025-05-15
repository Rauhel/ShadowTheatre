using UnityEngine;
using System.Diagnostics;
using System.IO;

public class GestureAppLauncher : MonoBehaviour
{
    public void LaunchGestureApp()
    {
        // 获取StreamingAssets路径
        string gestureAppDir = Path.Combine(Application.streamingAssetsPath, "gesture_app");
        string exePath = Path.Combine(gestureAppDir, "gesture_app.exe"); // 假设主程序名为gesture_app.exe

        if (File.Exists(exePath))
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(exePath)
            {
                WorkingDirectory = gestureAppDir,
                UseShellExecute = false,
                CreateNoWindow = true // 如果需要窗口可设为false
            };
            Process.Start(startInfo);
        }
        else
        {
            UnityEngine.Debug.LogError("Gesture app not found: " + exePath);
        }
    }
}
