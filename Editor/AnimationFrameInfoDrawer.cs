using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimatedKit
{
[CustomPropertyDrawer(typeof(AnimationFrameInfo))]
public class AnimationFrameInfoDrawer : PropertyDrawer
{
    // 存储每个列表的展开状态
    private Dictionary<string, bool> listExpandedStates = new Dictionary<string, bool>();
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 开始属性绘制
        EditorGUI.BeginProperty(position, label, property);
        
        // 计算属性高度
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float verticalSpacing = EditorGUIUtility.standardVerticalSpacing;
        
        // 绘制折叠箭头和标签
        Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        
        if (property.isExpanded)
        {
            // 计算内部属性的位置
            float currentY = position.y + lineHeight + verticalSpacing;
            
            // 获取所有序列化属性
            SerializedProperty nameProp = property.FindPropertyRelative("Name");
            SerializedProperty startFrameProp = property.FindPropertyRelative("StartFrame");
            SerializedProperty endFrameProp = property.FindPropertyRelative("EndFrame");
            SerializedProperty frameCountProp = property.FindPropertyRelative("FrameCount");
            SerializedProperty secondsProp = property.FindPropertyRelative("Seconds");
            SerializedProperty animatedEventsProp = property.FindPropertyRelative("animatedEvents");
            SerializedProperty isLoopProp = property.FindPropertyRelative("IsLoop");

            // 生成唯一的列表标识符
            string listKey = $"{property.propertyPath}.animatedEvents";
            if (!listExpandedStates.ContainsKey(listKey))
            {
                listExpandedStates[listKey] = true;
            }
            
            // 绘制每个属性
            Rect nameRect = new Rect(position.x, currentY, position.width, lineHeight);
            currentY += lineHeight + verticalSpacing;
            Rect isLoopRect = new Rect(position.x, currentY, position.width, lineHeight);
            currentY += lineHeight + verticalSpacing;
            Rect secondsRect = new Rect(position.x, currentY, position.width, lineHeight);
            currentY += lineHeight + verticalSpacing;
            Rect startFrameRect = new Rect(position.x, currentY, position.width, lineHeight);
            currentY += lineHeight + verticalSpacing;
            
            Rect endFrameRect = new Rect(position.x, currentY, position.width, lineHeight);
            currentY += lineHeight + verticalSpacing;
            
            Rect frameCountRect = new Rect(position.x, currentY, position.width, lineHeight);
            currentY += lineHeight + verticalSpacing;
            



            
            // 绘制属性字段
            EditorGUI.PropertyField(nameRect, nameProp);
            EditorGUI.PropertyField(isLoopRect, isLoopProp);
            EditorGUI.PropertyField(secondsRect, secondsProp);

            GUI.enabled = false; // 禁用 GUI 编辑

            EditorGUI.PropertyField(startFrameRect, startFrameProp);
            EditorGUI.PropertyField(endFrameRect, endFrameProp);
            EditorGUI.PropertyField(frameCountRect, frameCountProp);
            GUI.enabled = true; // 恢复 GUI 编辑

            // 绘制事件列表标题
            Rect eventsHeaderRect = new Rect(position.x, currentY, position.width, lineHeight);
            currentY += lineHeight + verticalSpacing;
            
            // 绘制事件列表
            Rect eventsListRect = new Rect(position.x, currentY, position.width, 0);
            float eventsListHeight = 0;
            // 绘制事件列表标题和折叠按钮
            Rect eventsFoldoutRect = new Rect(position.x, eventsHeaderRect.y, position.width - 60, lineHeight);
            Rect addEventButtonRect = new Rect(position.x + position.width - 150, eventsHeaderRect.y, 150, lineHeight);
            if (GUI.Button(addEventButtonRect, "添加新事件"))
            {
                animatedEventsProp.arraySize++;
            }
            listExpandedStates[listKey] = EditorGUI.Foldout(eventsFoldoutRect, listExpandedStates[listKey], $"动画事件列表(数量:{animatedEventsProp.arraySize})", true);
            // 绘制事件列表
            if (listExpandedStates[listKey])
            {
                for (int i = 0; i < animatedEventsProp.arraySize; i++)
                {
                    SerializedProperty eventElement = animatedEventsProp.GetArrayElementAtIndex(i);
                    var eventElementHeight = EditorGUI.GetPropertyHeight(eventElement);
                    Rect eventElementRect = new Rect(position.x, currentY, position.width, 
                        EditorGUI.GetPropertyHeight(eventElement));
                    
                    Rect elementBackgroundRect = new Rect(position.x - 2, currentY - 1, position.width + 4, 
                        EditorGUI.GetPropertyHeight(eventElement) + 2);
                    
                    // 绘制背景
                    EditorGUI.DrawRect(elementBackgroundRect, new Color(0, 1f, 0, 0.1f));
                    
                    // 绘制删除按钮
                    Rect deleteButtonRect = new Rect(position.x  , currentY+ eventElementHeight +2, 150, lineHeight);
                    
                    EditorGUI.PropertyField(eventElementRect, eventElement, 
                        new GUIContent($"事件 {i}"), true);
                    
                    if (GUI.Button(deleteButtonRect, $"删除 事件 {i}"))
                    {
                        animatedEventsProp.DeleteArrayElementAtIndex(i);
                        break; // 退出循环，避免索引错误
                    }
                    
                    currentY += eventElementHeight + verticalSpacing *2 + deleteButtonRect.height;
                    eventsListHeight += eventElementHeight + verticalSpacing;
                }
                
                eventsListRect.height = eventsListHeight;
            }
            // 按钮区域
            Rect buttonRect = new Rect(position.x, currentY, position.width, lineHeight);
            object targetObject = GetTargetObject(property);
            if (targetObject is AnimationFrameInfo trigger)
            {
                if (GUI.Button(buttonRect,trigger.isEditorPreviewing ? "StopPreview": "Preview"))
                {
                    trigger.isEditorPreviewing = !trigger.isEditorPreviewing;
                    trigger.OnEditorPreviewClick?.Invoke(trigger);
                }
            }
        }
        
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float verticalSpacing = EditorGUIUtility.standardVerticalSpacing;
        
        float height = lineHeight; // 主折叠行
        
        if (property.isExpanded)
        {
            // 5个基本属性字段
            height += (lineHeight + verticalSpacing) * 6;
            
            // 事件列表标题
            height += lineHeight + verticalSpacing;
            
            // 事件列表
            SerializedProperty animatedEventsProp = property.FindPropertyRelative("animatedEvents");
            string listKey = $"{property.propertyPath}.animatedEvents";
            
            if (listExpandedStates.ContainsKey(listKey) && listExpandedStates[listKey])
            {
                for (int i = 0; i < animatedEventsProp.arraySize; i++)
                {
                    SerializedProperty eventElement = animatedEventsProp.GetArrayElementAtIndex(i);
                    height += EditorGUI.GetPropertyHeight(eventElement) +EditorGUIUtility.singleLineHeight + verticalSpacing;
                }
            }
            
            // 主按钮（高度增加50%）
            height += lineHeight * 1.5f + verticalSpacing;
        }
        
        return height;
    }
    
    private object GetTargetObject(SerializedProperty property)
    {
        string path = property.propertyPath;
        object obj = property.serializedObject.targetObject;
        string[] elements = path.Split('.');
        string targetFieldName = elements[0];
        foreach (string element in elements)
        {
            if (element.Contains("["))
            {
                // 处理数组
                // string elementName = element.Substring(0, element.IndexOf("["));
                int index = int.Parse(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                obj = GetValue(obj, targetFieldName, index);
            }
        }
        return obj;
    }
    
    private object GetValue(object source, string name, int index = -1)
    {
        if (source == null) return null;
        
        System.Type type = source.GetType();
        
        if (index >= 0)
        {
            // 数组或列表
            System.Collections.IEnumerable enumerable = type.GetField(name).GetValue(source) as System.Collections.IEnumerable;
            if (enumerable != null)
            {
                System.Collections.IEnumerator enumerator = enumerable.GetEnumerator();
                int currentIndex = 0;
                while (enumerator.MoveNext())
                {
                    if (currentIndex == index)
                    {
                        return enumerator.Current;
                    }
                    currentIndex++;
                }
            }
            return null;
        }
        else
        {
            return type.GetField(name).GetValue(source);
        }
    }
}
}