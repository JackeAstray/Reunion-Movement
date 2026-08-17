using Microsoft.Win32;
using ReunionMovement.Common.Util;
using System;
using UnityEditor;
using UnityEngine;

namespace ReunionMovement.EditorTools
{
    [CustomEditor(typeof(DeadlineMgr))]
    public class DeadlineMgrEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);
            if (GUILayout.Button("彻底清除截止日期数据（编辑器 + 打包程序）", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "清除截止日期数据",
                    "将清除 DeadlineMgr 的本地数据：\n" +
                    "1. 编辑器 PlayerPrefs\n" +
                    "2. 打包程序(exe) 注册表记录 HKCU\\Software\\<公司>\\<产品>\n\n" +
                    "请先关闭正在运行的打包程序，否则退出时可能重新写入。",
                    "确定", "取消"))
                {
                    ClearAllDeadlineData();
                }
            }
        }

        [MenuItem("ReunionMovement/清除截止日期数据（编辑器 + 打包程序）")]
        private static void ClearDeadlineDataMenu()
        {
            if (EditorUtility.DisplayDialog(
                "清除截止日期数据",
                "将清除 DeadlineMgr 的本地数据（编辑器 PlayerPrefs + 打包程序 exe 注册表记录）。\n请先关闭正在运行的打包程序。",
                "确定", "取消"))
            {
                ClearAllDeadlineData();
            }
        }

        /// <summary>
        /// 同时清除编辑器 PlayerPrefs 和打包程序（Windows 独立版）注册表中的截止日期记录。
        /// 说明：编辑器与打包程序使用不同的 PlayerPrefs 存储位置，
        /// 只调 PlayerPrefs.DeleteKey 清不到 exe 的数据，需要额外删除注册表。
        /// </summary>
        private static void ClearAllDeadlineData()
        {
            // 1) 编辑器 PlayerPrefs
            PlayerPrefs.DeleteKey("Deadline_LastUtcTicks_v1");
            PlayerPrefs.DeleteKey("Deadline_LastUtcHash_v1");
            PlayerPrefs.Save();

            // 2) 打包程序(exe) 注册表记录
            var company = PlayerSettings.companyName;
            var product = PlayerSettings.productName;
            var path = @"Software\" + company + @"\" + product;

            bool removed = false;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(path, writable: true))
                {
                    if (key != null)
                    {
                        // 重要：Unity 的 PlayerPrefs 在注册表里会把键名加上 _h<哈希> 后缀存储，
                        // 例如 Deadline_LastUtcTicks_v1_h2694804774。
                        // 直接 DeleteValue("Deadline_LastUtcTicks_v1") 是删不到的，
                        // 必须按前缀匹配枚举删除。
                        var names = key.GetValueNames();
                        for (int i = 0; i < names.Length; i++)
                        {
                            if (names[i].StartsWith("Deadline_LastUtcTicks_v1", StringComparison.OrdinalIgnoreCase) ||
                                names[i].StartsWith("Deadline_LastUtcHash_v1", StringComparison.OrdinalIgnoreCase))
                            {
                                key.DeleteValue(names[i], throwOnMissingValue: false);
                                removed = true;
                                Debug.Log($"DeadlineMgr: 已删除注册表值: {names[i]}");
                            }
                        }
                    }
                }

                if (removed)
                {
                    Debug.Log($"DeadlineMgr: 已清除打包程序注册表记录: HKCU\\{path}");
                }
                else
                {
                    Debug.Log($"DeadlineMgr: 打包程序注册表中没有截止日期记录（可能从未运行过或已被清除）: HKCU\\{path}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"DeadlineMgr: 清除打包程序注册表失败: {ex.Message}");
            }

            EditorUtility.DisplayDialog(
                "完成",
                removed
                    ? $"已清除全部截止日期数据（含打包程序注册表记录）：\nHKCU\\{path}"
                    : "已清除编辑器 PlayerPrefs；打包程序注册表中无记录。",
                "OK");
        }
    }
}