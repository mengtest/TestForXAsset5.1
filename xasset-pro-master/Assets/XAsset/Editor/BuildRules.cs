﻿//
// BuildRules.cs
//
// Author:
//       MoMo的奶爸 <xasset@qq.com>
//
// Copyright (c) 2020 MoMo的奶爸
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation bundles (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace libx {
    public enum GroupBy {
        None,
        Explicit,   // 自定义 组名
        Filename,   // 文件名
        Directory,  // 文件夹
    }

    // 打包时的 Asset 信息
    [Serializable]
    public class AssetBuild {
        // 要打包的文件名
        // e.g. Assets/XAsset/Extend/TestImage/Btn_Tab1_n 1.png
        public string assetName; 
        // asset 的组名
        public string groupName;
        // ab包的名字
        public string bundleName = string.Empty;
        // 没有用到
        public int id;
        // 打包规则
        public GroupBy groupBy = GroupBy.Filename;
    }

    // 打包时的 Bundle 信息
    [Serializable]
    public class BundleBuild {
        public string assetBundleName;  // bundle 名
        public List<string> assetNames = new List<string>();    // 包含的 asset 名

        // 转换为 AssetBundleBuild
        public AssetBundleBuild ConvertToAssetBundleBuild() {
            return new AssetBundleBuild() {
                assetBundleName = assetBundleName,
                assetNames = assetNames.ToArray(),
            };
        }
    }

    // 打包时的 分包信息
    [Serializable]
    public class PatchBuild {
        public string name; // 分包名字
        public List<string> assetNameList = new List<string>();    // 分包包含的 asset 名字
    }

    public class BuildRules : ScriptableObject {
        // [asset名, ...]
        // 被多个 bundle 引用的 未设置过 bundleName 的 asset
        private readonly List<string> _duplicatedAssetList = new List<string>();

        // [asset名, HashSet<bundle名(可能有多个)>]
        // 主要用来计算 _unexplicitDict, _duplicatedList
        // asset 和 所属的 bundle(可能有多个)
        // 如果一个asset没有设置bundle且被多个bundle引用，那这个 asset 就会被加入到 _duplicated
        private readonly Dictionary<string, HashSet<string>> _asset2BundleHashSetDict = new Dictionary<string, HashSet<string>>();

        // 通过AssetBuild 转化得到
        //
        // [asset名, bundle名]
        // e.g. 一个 asset 一个
        // [Assets/XAsset/Extend/TestImage/Btn_Tab1_n 1.png, assets_xasset_extend_testimage]
        // e.g. 单个 bundle 依赖的 的没有  bundle 的 asset
        // [Assets/XAsset/Extend/TestCommon/Btn_User_h 2.png, children_assets_xasset_extend_testprefab3]
        // e.g. 多个 bundle 依赖的 没有设置 bundle 的 asset
        // [Assets/XAsset/Extend/TestCommon/Btn_User_h 1.png, shared_assets_xasset_extend_testcommon]
        private readonly Dictionary<string, string> _asset2BundleDict = new Dictionary<string, string>();

        // [asset名, bundle名]
        // 通过分析依赖查找出来的没有显式设置 bundle 名的资源
        // bundle 名 为最后分析的那一个
        // // e.g. [Assets/XAsset/Extend/TestCommon/Btn_User_h 1.png, children_assets_extend_testprefab"]
        private readonly Dictionary<string, string> _unexplicitAsset2BundleDict = new Dictionary<string, string>();

        [Header("版本号")]
        // 
        [Tooltip("构建的版本号")] public int build;
        public int major;
        public int minor;

        // 主要用来在 编辑器下 查找 场景文件
        [Tooltip("场景文件夹")]
        public string[] scenesFolders = new string[] { "Assets/XAsset" };

        [Header("自动分包分组配置")]
        [Tooltip("是否自动记录资源的分包分组")]
        public bool autoRecord;
        [Tooltip("按目录自动分组")]
        public string[] autoGroupByDirectories = new string[0];

        [Header("编辑器提示选项")]
        [Tooltip("检查加载路径大小写是否存在")]
        public bool validateAssetPath;

        [Header("首包内容配置")]
        [Tooltip("是否整包")] public bool allAssetsToBuild;

        // 必须在这里填写分包的名字, 才能在打包时 自动 把 bundle 复制到 StreamingAssets中
        [Tooltip("首包包含的分包")] public string[] patchNameInBuild = new string[0];

        [Tooltip("BuildPlayer的时候被打包的场景")] public SceneAsset[] scenesInBuild = new SceneAsset[0];

        [Header("AB打包配置")]
        // 所有 AB 包的 扩展名
        [Tooltip("AB的扩展名")] public string extension = "";
        public bool nameByHash;
        [Tooltip("打包AB的选项")] public BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression;

        [Header("缓存数据")]


        // 这里有记录的asset 才会打包
        [Tooltip("所有要打包的资源")]
        public List<AssetBuild> assetBuildList = new List<AssetBuild>();

        // 只在这里有记录的 asset 不会被打包, 这里只是用来分包的
        [Tooltip("所有分包")]
        public List<PatchBuild> patchBuildList = new List<PatchBuild>();

        // 生成 ab 包后, 这里才会有记录
        [Tooltip("所有打包的资源")]
        public List<BundleBuild> bundleBuildList = new List<BundleBuild>();

        // 当前场景名字
        public string currentScene;

        public void BeginSample() {
        }

        public void EndSample() {

        }


        // BuildRules.OnLoadAsset
        public void OnLoadAsset(string assetPath) {
            // 开启了自动记录 并且是 开发模式
            if (autoRecord && Assets.development) {
                GenAssetBuild(assetPath, GetGroupBy(assetPath));
            } else {
                // 校验文件路径
                if (validateAssetPath) {
                    if (assetPath.Contains("Assets")) {
                        if (File.Exists(assetPath)) {
                            if (!assetBuildList.Exists(asset => asset.assetName.Equals(assetPath))) {
                                EditorUtility.DisplayDialog("文件大消息不匹配", assetPath, "确定");
                            }
                        } else {
                            EditorUtility.DisplayDialog("资源不存在", assetPath, "确定");
                        }
                    }
                }
            }
        }

        // 获取 asset 的 分组类型
        private GroupBy GetGroupBy(string assetPath) {
            // 默认 GroupBy.Filename
            GroupBy groupBy = GroupBy.Filename;
            // 获取文件夹路径
            string dir = Path.GetDirectoryName(assetPath).Replace("\\", "/");

            // 如果asset 在 自动分组 文件夹里, 就 GroupBy.Directory
            if (autoGroupByDirectories.Length > 0) {
                foreach (string groupWithDir in autoGroupByDirectories) {
                    if (groupWithDir.Contains(dir)) {
                        groupBy = GroupBy.Directory;
                        break;
                    }
                }
            }

            return groupBy;
        }

        // BuildRules.OnUnloadAsset
        // 卸载资源，暂无实现
        public void OnUnloadAsset(string assetPath) {

        }

        #region API

        // 生成 AsssetBuild
        public AssetBuild GenAssetBuild(string path, GroupBy groupBy = GroupBy.Filename, string group = null) {
            // 在 assetBuildList 里查找 有没有记录
            AssetBuild tempAssetBuild = this.assetBuildList.Find(assetBuild => assetBuild.assetName.Equals(path));

            if (tempAssetBuild == null) {
                tempAssetBuild = new AssetBuild();
                // 
                tempAssetBuild.assetName = path;
                this.assetBuildList.Add(tempAssetBuild);
            }

            // 自定义组名
            if (groupBy == GroupBy.Explicit) {
                tempAssetBuild.groupName = group;
            }


            // e.g. Assets/XAsset/Demo/Scenes/Title.unity
            if (IsScene(path)) {
                // e.g. Title
                currentScene = Path.GetFileNameWithoutExtension(path);
            }

            // 分组类型
            tempAssetBuild.groupBy = groupBy;

            // 开启了自动记录 并且是 开发者模式
            if (autoRecord && Assets.development) {
                // 分包
                PatchAsset(path);
            }

            return tempAssetBuild;
        }

        // 分包, 按照当前场景
        public void PatchAsset(string path) {
            // e.g. Title
            string patchName = currentScene;

            // 查找分包记录
            PatchBuild tempPatch = patchBuildList.Find(patch => patch.name.Equals(patchName));

            if (tempPatch == null) {
                tempPatch = new PatchBuild();
                tempPatch.name = patchName;
                patchBuildList.Add(tempPatch);
            }

            // 存在这个文件
            if (File.Exists(path)) {
                // 分包不包含这个文件
                if (!tempPatch.assetNameList.Contains(path)) {
                    // 添加到 分包 的 assetList 里
                    tempPatch.assetNameList.Add(path);
                }
            }
        }

        // build 版本 加1
        public string AddVersion() {
            build = build + 1;
            return GetVersion();
        }

        // 获取 Version
        public string GetVersion() {
            Version version = new Version(major, minor, build);
            return version.ToString();
        }
        // BuildRules.Analyze()
        // 解析 Rules.asset
        public void Analyze() {
            // 清空 _unexplicits, _tracker, _duplicated, _asset2BundleDict
            Clear();
            // 为 Rules.asset 下的 每个 AssetBuild 添加 BundleName,并转为换 Asset2BundleDict  里的记录 
            AddBundleNameToAssetBuildAndConvertToAsset2BundleDict();
            // 获取依赖, 分析出  _tracker, _unexplicits, _duplicated
            AnalysisAssets();
            // 优化资源
            OptimizeAssetsFromUnexplicitDict();
            // 保存 BuildRules
            Save();
        }

        // List<BundleBuild> 转换为 AssetBundleBuild[]
        public AssetBundleBuild[] GetAssetBundleBuildArray() {
            return bundleBuildList.ConvertAll(
                delegate (BundleBuild input) {
                    return input.ConvertToAssetBundleBuild();
                }
            ).ToArray();
        }

        #endregion

        #region Private

        private string GetGroupNameByAssetBuild(AssetBuild assetBuild) {
            // isChildren = false
            // isShared = false
            return GetGroupName(assetBuild.groupBy, assetBuild.assetName, assetBuild.groupName, isChildren: false, isShared: false);
        }

        // 检查是否是符合规则的文件
        internal bool ValidateAsset(string assetName) {
            // 忽略 不在 Assets/ 下的文件
            if (!assetName.StartsWith("Assets/"))
                return false;

            // 获取后缀名
            string ext = Path.GetExtension(assetName).ToLower();
            // 不处理 .dll, .cs, .meta, .js, .boo
            return ext != ".dll" 
                && ext != ".cs" 
                && ext != ".meta" 
                && ext != ".js" 
                && ext != ".boo";
        }

        // 是否是场景
        private bool IsScene(string assetName) {
            return assetName.EndsWith(".unity");
        }

        // 获取文件的分组名
        private string GetGroupName(GroupBy groupBy, string assetName, string groupName = null, bool isChildren = false, bool isShared = false) {
            // 特殊处理 shader 的 groupBy 为 GroupBy.Explicit
            // 特殊处理 shader 的 groupName  固定为 shaders
            if (assetName.EndsWith(".shader")) {
                groupName = "shaders";
                groupBy = GroupBy.Explicit;
                isChildren = false;
            // 特殊处理 场景的 groupBy 为 GroupBy.Filename
            } else if (IsScene(assetName)) {
                groupBy = GroupBy.Filename;
            }

            switch (groupBy) {
                case GroupBy.Explicit:
                    break;
                // 只会得到 形如 _Title 的 groupName
                case GroupBy.Filename: {
                        // 只获取 文件名 e.g. Title
                        string assetNameNoExt = Path.GetFileNameWithoutExtension(assetName);
                        // 永远是 ""
                        string directoryName = Path.GetDirectoryName(assetNameNoExt).Replace("\\", "/").Replace("/", "_");
                        // groupName 通过 组合得到 e.g. _Title
                        groupName = directoryName + "_" + assetNameNoExt;
                    }
                    break;
                // 这里只会得到 形如 Assets_XAsset_Demo_UI_Sprites_Title 的 groupName
                case GroupBy.Directory: {
                        // 将 路径中的 \\,/ 转换为 _
                        // e.g. Assets/XAsset/Demo/UI/Sprites/Title/Progress1.png
                        //      Assets_XAsset_Demo_UI_Sprites_Title
                        string directoryName = Path.GetDirectoryName(assetName).Replace("\\", "/").Replace("/", "_");
                        // groupName 是 文件夹名
                        groupName = directoryName;
                        break;
                    }
            }

            // 只被单个 bundle 引用
            if (isChildren) {
                return "children_" + groupName;
            }

            // 被多个 bundle 引用
            if (isShared) {
                groupName = "shared_" + groupName;
            }

            // 是否转 hash
            return (nameByHash ? Utility.GetMD5Hash(groupName) : groupName.TrimEnd('_').ToLower()) + extension;
        }

        // 记录 asset 所属的 bundles(可能有多个)
        // 添加 _tracker 记录
        // 添加 _unexplicits 记录
        // 添加 _duplicated 记录
        private void Track(string dependencyAssetName, string bundleName) {
            HashSet<string> bundleNameHashSet;

            if (!_asset2BundleHashSetDict.TryGetValue(dependencyAssetName, out bundleNameHashSet)) {
                bundleNameHashSet = new HashSet<string>();
                _asset2BundleHashSetDict.Add(dependencyAssetName, bundleNameHashSet);
            }

            // 同一个资源可能被多个
            bundleNameHashSet.Add(bundleName);

            // 在 Asset2BundleDict 中 查找 asset 对应的 bundle
            string findedBundleNameInAsset2BundleDict;
            _asset2BundleDict.TryGetValue(dependencyAssetName, out findedBundleNameInAsset2BundleDict);

            // _asset2BundleDict 里找不到 这个 bundle名， 因为没有设置
            // 是分析依赖时找出来的
            if (string.IsNullOrEmpty(findedBundleNameInAsset2BundleDict)) {
                // e.g. [Assets/XAsset/Extend/TestCommon/Btn_User_h 1.png, children_assets_extend_testprefab"]
                // groupName = bundleName
                //
                // isChildren = true
                // isShared = false
                // 
                // 情况1. assetName 只被一处引用,
                //  1. A 依赖 assetName 且 assetName 没有 对应的 bundleName, bundleName = A, groupName = children_A
                //  只被一处引用的情况 不会在 OptimizeAsset 中处理 
                //
                // 情况2. assetName 被多处引用,按照分析的顺序
                //  1. A 依赖 assetName 且 assetName 没有 对应的 bundleName, bundleName = A, groupName = children_A
                //  2. B 依赖 assetName 且 assetName 没有 对应的 bundleName, bundleName = B, groupName = children_B (覆盖了上一条, 且这个名字只是暂时的)
                //  覆盖的情况 会在 OptimizeAsset 中处理
                _unexplicitAsset2BundleDict[dependencyAssetName] = GetGroupName(GroupBy.Explicit, dependencyAssetName, bundleName, isChildren: true, isShared: false);

                // 同一个资源被多个不同的 未显式设置bundleName 的 bundle 引用, 添加到 _duplicatedList
                if (bundleNameHashSet.Count > 1) {
                    _duplicatedAssetList.Add(dependencyAssetName);
                }
            }
        }

        // 将 assetName 和 assetBundleName 的 对应关系 存储到 _asset2Bundles
        private void ConvertAssetBuildToAsset2BundleDict(string assetName, string assetBundleName) {
            // 如果 是场景文件, 要重新
            if (IsScene(assetName)) {
                assetBundleName = GetGroupNameByAssetBuild(
                    // 1. 根据场景
                    GenAssetBuild(assetName)
                );
            }

            // e.g. [Assets/XAsset/Extend/TestImage/Btn_Tab1_n 1.png, assets_xasset_extend_testimage]
            // 多个 不同的 assetName  可能有相同的 assetBundleName
            _asset2BundleDict[assetName] = assetBundleName;
        }

        private void Clear() {
            _unexplicitAsset2BundleDict.Clear();
            _asset2BundleHashSetDict.Clear();
            _duplicatedAssetList.Clear();
            _asset2BundleDict.Clear();
        }

        // 将 一对一的  [assetName, bundleName] 转化为 [bundleName, List<assetName>]
        private Dictionary<string, List<string>> GetBundle2AssetListDictFromAsset2BundleDict() {
            Dictionary<string, List<string>> bundle2AssetListDict = new Dictionary<string, List<string>>();

            foreach (KeyValuePair<string, string> item in _asset2BundleDict) {
                string bundleName = item.Value;
                List<string> assetNamelist;

                if (!bundle2AssetListDict.TryGetValue(bundleName, out assetNamelist)) {
                    assetNamelist = new List<string>();
                    bundle2AssetListDict[bundleName] = assetNamelist;
                }

                if (!assetNamelist.Contains(item.Key))
                    assetNamelist.Add(item.Key);
            }

            return bundle2AssetListDict;
        }

        // 保存到 Rules.asset 中
        private void Save() {
            bundleBuildList.Clear();

            Dictionary<string, List<string>> bundle2AssetListDict = GetBundle2AssetListDictFromAsset2BundleDict();

            // 重新生成  Rules.asset 中的 BundleBuild
            foreach (KeyValuePair<string, List<string>> item in bundle2AssetListDict) {
                BundleBuild bundleBuild = new BundleBuild() {
                    assetBundleName = item.Key,
                    assetNames = item.Value,
                };

                bundleBuildList.Add(bundleBuild);
            }


            // 移除 PatchBuild 中不存在的 asset
            foreach (PatchBuild patchBuild in patchBuildList) {
                for (var i = 0; i < patchBuild.assetNameList.Count; ++i) {
                    string asset = patchBuild.assetNameList[i];
                    if (!File.Exists(asset)) {
                        patchBuild.assetNameList.RemoveAt(i);
                        --i;
                    }
                }
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        // 为 Rules.asset 下的 每个 AssetBuild 添加 BundleName,并转为换 Asset2BundleDict  里的记录
        private void AddBundleNameToAssetBuildAndConvertToAsset2BundleDict() {
            List<AssetBuild> assetBuildListWillAddBundleName = new List<AssetBuild>();

            // D:\\Projects\\UnityProjects\\TestForXAsset5.1\\xasset-pro-master
            int len = Environment.CurrentDirectory.Length + 1;

            // 处理路径大小写问题
            for (int index = 0; index < this.assetBuildList.Count; index++) {
                AssetBuild assetBuild = this.assetBuildList[index];
                // 通过 文件名  读取文件信息
                FileInfo fileInfo = new FileInfo(assetBuild.assetName);

                // 存在文件且符合规则
                if (fileInfo.Exists && ValidateAsset(assetBuild.assetName)) {
                    // FullName e.g. D:\\Projects\\UnityProjects\\TestForXAsset5.1\\xasset-pro-master\\Assets\\XAsset\\Extend\\TestImage\\Btn_Tab1_n 1.png
                    // relativePath e.g. Assets/XAsset/Extend/TestImage/Btn_Tab1_n 1.png
                    // 转换为 能识别的相对路径
                    string relativePath = fileInfo.FullName.Substring(len).Replace("\\", "/");

                    if (!relativePath.Equals(assetBuild.assetName)) {
                        Debug.LogWarningFormat("检查到路径大小写不匹配！输入：{0}实际：{1}，已经自动修复。", assetBuild.assetName, relativePath);
                        assetBuild.assetName = relativePath;
                    }
                    
                    assetBuildListWillAddBundleName.Add(assetBuild);
                }
            }

            // 给 AssetBuild 添加 bundleName
            for (int i = 0; i < assetBuildListWillAddBundleName.Count; i++) {

                AssetBuild assetBuild = assetBuildListWillAddBundleName[i];

                // 不给 GroupBy.None 的 AssetBuild 添加 bundleName
                if (assetBuild.groupBy == GroupBy.None) {
                    continue;
                }

                // 获取 asset 的 GroupName, 设置为 AssetBuild.bundleName
                assetBuild.bundleName = GetGroupNameByAssetBuild(assetBuild);


                // 将 assetName 和 assetBundleName 的 对应关系 存储到 _asset2Bundles
                ConvertAssetBuildToAsset2BundleDict(assetBuild.assetName, assetBuild.bundleName);
            }


            // assetBuildListWillAddAssetNameAndBundleName 赋值给 AssetBuildList
            this.assetBuildList = assetBuildListWillAddBundleName;
        }

        // 分析
        private void AnalysisAssets() {
            // 获取 [bundleName, List<assetName>]
            Dictionary<string, List<string>> bundle2AssetDict = GetBundle2AssetListDictFromAsset2BundleDict();

            int i = 0;
            foreach (KeyValuePair<string, List<string>> item in bundle2AssetDict) {
                string bundleName = item.Key;

                string tips = string.Format("分析依赖{0}/{1}", i, bundle2AssetDict.Count);

                if (EditorUtility.DisplayCancelableProgressBar(tips, bundleName, i / (float)bundle2AssetDict.Count))
                    break;

                // 获取 一个 bundle 里的 所有 assetName
                // [assetName, ...]
                string[] assetPaths = item.Value.ToArray();

                // 获取 [assetName, ...] 的依赖文件名（包括自身）
                string[] dependencyAssetNameArray = AssetDatabase.GetDependencies(assetPaths, true);

                if (dependencyAssetNameArray.Length > 0)
                    foreach (string dependencyAssetName in dependencyAssetNameArray) {
                        // 不 Track 文件夹, .spriteatlas, .giparams, LightingData.asset
                        if (Directory.Exists(dependencyAssetName)
                            || dependencyAssetName.EndsWith(".spriteatlas")
                            || dependencyAssetName.EndsWith(".giparams")
                            || dependencyAssetName.EndsWith("LightingData.asset")) {
                            continue;
                        }

                        // 验证文件
                        if (ValidateAsset(dependencyAssetName)) {
                            Track(dependencyAssetName, bundleName);
                        }
                    }
                i++;
            }
        }

        // 优化多个文件
        private void OptimizeAssetsFromUnexplicitDict() {

            // 只在 未显式设置 bundleName 的字典里查找
            foreach (KeyValuePair<string, string> item in _unexplicitAsset2BundleDict) {
                // 查找 _asset2BundleHashSetDict 里的 asset 只属于一个 bundle, 添加到 _asset2BundleDict
                // e.g. [Assets/XAsset/Extend/TestCommon/Btn_User_h 2.png, children_assets_xasset_extend_testprefab3]
                if (_asset2BundleHashSetDict[item.Key].Count < 2) {
                    _asset2BundleDict[item.Key] = item.Value;
                }
            }

            // 从这里开始 _trackerDict 就没有用了, 可以清掉. add by 黄鑫
            _asset2BundleHashSetDict.Clear();
            // 从这里开始 _unexplicitDict 就没有用过了, 可以清掉. add by 黄鑫
            _unexplicitAsset2BundleDict.Clear();


            for (int i = 0, max = _duplicatedAssetList.Count; i < max; i++) {
                string assetName = _duplicatedAssetList[i];
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冗余{0}/{1}", i, max), assetName,
                    i / (float)max)) break;

                // 优化被多个bundle 引用的 未设置过 bundle 的 asset
                GenSharedBundleByDirectoryName(assetName);
            }

            // 从这里开始 _duplicatedList 就没有用过了, 可以清掉. add by 黄鑫
            _duplicatedAssetList.Clear();
        }

        // 优化被多个bundle 引用的 未设置过 bundle 的 asset
        private void GenSharedBundleByDirectoryName(string assetName) {
            // 添加到 _asset2BundleDict
            // isChildren = false
            // isShared = true
            // e.g. [Assets/XAsset/Extend/TestCommon/Btn_User_h 1.png, shared_assets_xasset_extend_testcommon]
            _asset2BundleDict[assetName] = GetGroupName(GroupBy.Directory, assetName, null, isChildren: false, isShared: true);
        }

        #endregion
    }
}