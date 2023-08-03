﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;

namespace GBG.AssetQuickAccess.Editor
{
    public class AssetQuickAccessWindow : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Tools/Bamboo/Asset Quick Access Window")]
        [MenuItem("Window/Asset Management/Asset Quick Access Window")]
        public static void Open()
        {
            var window = GetWindow<AssetQuickAccessWindow>("Asset Quick Access");
            window.minSize = new Vector2(400, 120);
        }


        private void OnEnable()
        {
            ConvertOldData();
            CreateView();
            _isViewDirty = true;

            AssemblyReloadEvents.afterAssemblyReload -= RefreshData;
            AssemblyReloadEvents.afterAssemblyReload += RefreshData;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.afterAssemblyReload -= RefreshData;
        }

        private void Update()
        {
            if (_isViewDirty)
            {
                _assetListView.RefreshItems();
                _isViewDirty = false;
            }
        }

        private void OnProjectChange()
        {
            _isViewDirty = true;
        }

        private void ConvertOldData()
        {
            // Delete version 1 data(Conversion not supported)
            EditorPrefs.DeleteKey("GBG.AssetQuickAccess.SettingsPrefs");

            // Convert Version 2 data to Version 3
            var prefsKey = "GBG.AssetQuickAccess.SettingsPrefs@" +
                           Application.companyName + "@" + Application.productName;
            if (EditorPrefs.HasKey(prefsKey))
            {
                var guidPrefs = EditorPrefs.GetString(prefsKey, "");
                var guids = guidPrefs.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < guids.Length; i++)
                {
                    var guid = guids[i];
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    AssetQuickAccessSettings.AddAsset(assetPath);
                }

                EditorPrefs.DeleteKey(prefsKey);
            }
        }

        private void CreateView()
        {
            // Root canvas
            // Can not add drag and drop manipulator to rootVisualElement directly,
            // so we need an extra visual element(_rootCanvas) to handle drag and drop events
            _rootCanvas = new VisualElement();
            _rootCanvas.StretchToParentSize();
            rootVisualElement.Add(_rootCanvas);
            var dragDropManipulator = new DragAndDropManipulator(_rootCanvas);
            dragDropManipulator.OnDragAndDropAssets += OnDragAndDropAssets;

            // Asset list view
            _assetListView = new ListView
            {
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                makeItem = CreateNewAssetListItem,
                bindItem = BindAssetListItem,
                unbindItem = UnbindAssetListItem,
                itemsSource = AssetQuickAccessSettings.GetGuids(),
                selectionType = SelectionType.None,
                style =
                {
                    flexGrow = 1,
                    marginTop = 2,
                    minHeight = 40,
                }
            };
            _assetListView.itemIndexChanged += OnReorderAsset;
            _rootCanvas.Add(_assetListView);

            // Tool tips
            var tipsText = new Label
            {
                text = "Drag and drop the asset here to add a new item.",
                style =
                {
                    unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter),
                    textOverflow = new StyleEnum<TextOverflow>(TextOverflow.Ellipsis),
                    height = 36
                }
            };
            _rootCanvas.Add(tipsText);

            // Find asset by guid/path
            var findAssetContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = 32,
                    minHeight = 32,
                    paddingTop = 3,
                    paddingBottom = 3,
                },
            };
            _rootCanvas.Add(findAssetContainer);
            var assetUrlField = new TextField("Find Asset")
            {
                style =
                {
                    flexGrow = 1,
                },
                labelElement =
                {
                    style =
                    {
                        width = 68,
                        minWidth = 68,
                        unityTextAlign = TextAnchor.MiddleCenter,
                    }
                }
            };
            findAssetContainer.Add(assetUrlField);
            var findAssetButton = new Button(() =>
                {
                    var url = assetUrlField.value;
                    var filePath = string.Empty;
                    if (!string.IsNullOrEmpty(url))
                    {
                        if (url.StartsWith("Assets", StringComparison.OrdinalIgnoreCase)) filePath = url;
                        else filePath = AssetDatabase.GUIDToAssetPath(url);
                    }
                    var asset = AssetDatabase.LoadAssetAtPath<UObject>(filePath);
                    if (asset)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                    else
                    {
                        ShowNotification(new GUIContent($"Can not find asset with guid or path '{url}'."));
                    }
                })
            {
                text = "Find",
            };
            findAssetContainer.Add(findAssetButton);
        }

        private void RefreshData()
        {
            // Fix #1
            EditorApplication.delayCall += () =>
            {
                AssetQuickAccessSettings.Refresh();
                _isViewDirty = true;
            };
        }


        #region Asset List View

        private VisualElement _rootCanvas;

        private bool _isViewDirty;

        private ListView _assetListView;

        private static Texture _warningTexture;


        private VisualElement CreateNewAssetListItem()
        {
            var view = new AssetItemView();
            view.OnWantsToRemoveAssetItem += RemoveAsset;

            return view;
        }

        private void BindAssetListItem(VisualElement element, int index)
        {
            var view = (AssetItemView)element;
            var assetHandle = AssetQuickAccessSettings.GetAssetHandle(index);
            view.AssetHandle = assetHandle;
            view.Title.text = assetHandle.GetDisplayName();
            view.tooltip = AssetDatabase.GetAssetPath(assetHandle.Asset);
            Texture iconTex = AssetPreview.GetMiniThumbnail(assetHandle.Asset);
            if (!iconTex)
            {
                if (!_warningTexture)
                {
                    _warningTexture = EditorGUIUtility.IconContent("Warning@2x").image;
                }

                iconTex = _warningTexture;
            }
            view.Icon.image = iconTex;
        }

        private void UnbindAssetListItem(VisualElement element, int index)
        {
            var view = (AssetItemView)element;
            view.AssetHandle = null;
        }

        private void OnReorderAsset(int fromIndex, int toIndex)
        {
            AssetQuickAccessSettings.ForceSave();
        }

        private void RemoveAsset(AssetHandle assetHandle)
        {
            _isViewDirty |= AssetQuickAccessSettings.RemoveAsset(assetHandle);
        }

        private void OnDragAndDropAssets(IList<string> assetPaths)
        {
            for (int i = 0; i < assetPaths.Count; i++)
            {
                _isViewDirty |= AssetQuickAccessSettings.AddAsset(assetPaths[i]);
            }
        }

        #endregion


        #region Custom menu

        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Clear All Items"), false, () =>
            {
                AssetQuickAccessSettings.ClearAllAssets();
                _isViewDirty = true;
            });
            menu.AddItem(new GUIContent("Print Guids"), false, AssetQuickAccessSettings.PrintGuids);
        }

        #endregion
    }
}
