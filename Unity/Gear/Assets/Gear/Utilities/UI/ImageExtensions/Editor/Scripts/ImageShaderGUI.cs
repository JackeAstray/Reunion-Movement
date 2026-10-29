using UnityEditor;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

using Gear.Common;
namespace Gear.EditorTools.ImageExtensions
{
    public class ImageShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox("此处无需修改。在层次结构中选择一个ImageEx组件，然后修改检查器中的值。", MessageType.Info);
            EditorGUI.BeginDisabledGroup(true);
            base.OnGUI(materialEditor, properties);
            EditorGUI.EndDisabledGroup();
        }
    }
}