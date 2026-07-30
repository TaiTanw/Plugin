using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 本文件是 RetinarBatchModelBuilder 的 partial 分文件，只负责一件事：
//   生成交付物里的 06_docs/asset_info.xlsx。
//
// 为什么拆出来：
//   xlsx 本质是一个 zip 包，里面装着一堆 OOXML。这里是完全手写的实现——
//   拼 sheet XML、拼 styles、拼 content types、算列名、处理单元格合并、
//   往模板里回填单元格……近 500 行代码，和"模型规范化 / 打包"这件事没有任何关系，
//   原来却和主流程挤在同一个 2700 行的文件里。
//   拆开之后：
//     - 改表格内容（BuildResourceInfoRows / BuildAcceptanceRows / BuildFileChecklistRows）
//       只需要动这个文件，不会误碰打包流程；
//     - 反过来调打包流程时，也不用在几百行 XML 字符串里翻找。
//
// 用 partial 而不是新建一个独立类的原因：
//   这里要访问 GeneratedAsset / AssetStats（都是主类里的 private 嵌套结构体）和
//   AssetInfoTemplatePath 常量。用 partial 可以零改动地拆开，不必为了拆文件
//   把这些类型的可见性放大到 internal/public——放大可见性是纯粹的副作用，
//   会让别的代码有机会依赖上本该是内部实现的东西。
//
// 这个文件只做"数据 -> xlsx 字节"的转换，不读写 AssetDatabase，也不做任何校验。
// =====================================================================================
public static partial class RetinarBatchModelBuilder
{
    private static void WriteAssetInfoWorkbook(GeneratedAsset asset, string outputPath)
    {
        outputPath = GetWritableWorkbookPath(outputPath);

        string templateDiskPath = ResolveAssetInfoTemplatePath();
        if (!string.IsNullOrEmpty(templateDiskPath))
        {
            File.Copy(templateDiskPath, outputPath, true);
            UpdateTemplateWorkbook(outputPath, asset);
            return;
        }

        Debug.LogWarning("Asset info template not found. Falling back to generated workbook. " +
            "预期位置: " + AssetInfoTemplatePath + "（也已在全工程按文件名搜索过）。" +
            "回退生成的工作簿只有本工具自己排的版式，不是交付方给的模板版式，交付前请确认是否可接受。");
        using (var stream = new FileStream(outputPath, FileMode.CreateNew))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            AddEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            AddEntry(archive, "_rels/.rels", BuildRootRelsXml());
            AddEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
            AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
            AddEntry(archive, "xl/styles.xml", BuildStylesXml());
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildSheetXml(BuildResourceInfoRows(asset), 4));
            AddEntry(archive, "xl/worksheets/sheet2.xml", BuildSheetXml(BuildAcceptanceRows(asset), 5));
            AddEntry(archive, "xl/worksheets/sheet3.xml", BuildSheetXml(BuildFileChecklistRows(asset), 5));
            AddEntry(archive, "docProps/core.xml", BuildCorePropsXml());
            AddEntry(archive, "docProps/app.xml", BuildAppPropsXml());
        }
    }

    /// <summary>
    /// 找到模板 xlsx 在磁盘上的绝对路径，找不到返回 null。
    ///
    /// 为什么不能只认 AssetInfoTemplatePath 这个常量：
    ///   那个常量写的是 "Assets/Retinar/Templates/..."，前提是插件整体就放在 Assets/Retinar 下。
    ///   本工程把插件收纳到了 Assets/Plugin/RetinarBatchBuilder_Share/Assets/Retinar/，
    ///   于是常量路径永远命中不了，每次打包都静默回退成自己生成的版式——
    ///   而回退版式和交付方模板并不是一回事，属于"看起来成功了但交错东西"的隐患。
    ///   所以常量只当默认位置，命中不了就按文件名在全工程搜一遍。
    /// </summary>
    private static string ResolveAssetInfoTemplatePath()
    {
        string expectedDiskPath = Path.Combine(Directory.GetCurrentDirectory(), AssetInfoTemplatePath);
        if (File.Exists(expectedDiskPath))
        {
            return expectedDiskPath;
        }

        string templateFileName = Path.GetFileNameWithoutExtension(AssetInfoTemplatePath);
        foreach (string guid in AssetDatabase.FindAssets(templateFileName))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string diskPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (File.Exists(diskPath))
            {
                return diskPath;
            }
        }

        return null;
    }

    private static void UpdateTemplateWorkbook(string workbookPath, GeneratedAsset asset)
    {
        using (var stream = new FileStream(workbookPath, FileMode.Open, FileAccess.ReadWrite))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
        {
            UpdateWorksheetEntry(archive, "xl/worksheets/sheet1.xml", BuildCellMap(BuildResourceInfoRows(asset)));
            UpdateWorksheetEntry(archive, "xl/worksheets/sheet2.xml", BuildCellMap(BuildAcceptanceRows(asset)));
            UpdateWorksheetEntry(archive, "xl/worksheets/sheet3.xml", BuildCellMap(BuildFileChecklistRows(asset)));
        }
    }

    private static Dictionary<string, string> BuildCellMap(List<string[]> rows)
    {
        var cells = new Dictionary<string, string>();
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            string[] row = rows[rowIndex];
            for (int columnIndex = 0; columnIndex < row.Length; columnIndex++)
            {
                string value = row[columnIndex];
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                cells[ColumnName(columnIndex + 1) + (rowIndex + 1)] = value;
            }
        }

        return cells;
    }

    private static void UpdateWorksheetEntry(ZipArchive archive, string entryPath, Dictionary<string, string> values)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryPath);
        if (entry == null)
        {
            Debug.LogWarning("Worksheet entry not found in template: " + entryPath);
            return;
        }

        string xml;
        using (Stream stream = entry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            xml = reader.ReadToEnd();
        }

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument document = XDocument.Parse(xml);
        XElement sheetData = document.Root.Element(ns + "sheetData");
        if (sheetData == null)
        {
            Debug.LogWarning("sheetData not found in template worksheet: " + entryPath);
            return;
        }

        foreach (KeyValuePair<string, string> pair in values)
        {
            SetInlineStringCell(sheetData, ns, pair.Key, pair.Value);
        }

        entry.Delete();
        ZipArchiveEntry newEntry = archive.CreateEntry(entryPath);
        using (Stream stream = newEntry.Open())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            document.Save(writer, SaveOptions.DisableFormatting);
        }
    }

    private static void SetInlineStringCell(XElement sheetData, XNamespace ns, string cellReference, string value)
    {
        int rowNumber = GetRowNumber(cellReference);
        XElement row = sheetData.Elements(ns + "row")
            .FirstOrDefault(element => (int?)element.Attribute("r") == rowNumber);

        if (row == null)
        {
            row = new XElement(ns + "row", new XAttribute("r", rowNumber));
            sheetData.Add(row);
        }

        XElement cell = row.Elements(ns + "c")
            .FirstOrDefault(element => (string)element.Attribute("r") == cellReference);

        if (cell == null)
        {
            cell = new XElement(ns + "c", new XAttribute("r", cellReference));
            row.Add(cell);
        }

        XAttribute style = cell.Attribute("s");
        cell.RemoveAttributes();
        cell.Add(new XAttribute("r", cellReference));
        if (style != null)
        {
            cell.Add(new XAttribute("s", style.Value));
        }

        cell.Add(new XAttribute("t", "inlineStr"));
        cell.RemoveNodes();
        cell.Add(new XElement(ns + "is", new XElement(ns + "t", value)));
    }

    private static int GetRowNumber(string cellReference)
    {
        var digits = new string(cellReference.Where(char.IsDigit).ToArray());
        int.TryParse(digits, out int rowNumber);
        return rowNumber;
    }

    private static string GetWritableWorkbookPath(string outputPath)
    {
        if (!File.Exists(outputPath))
        {
            return outputPath;
        }

        try
        {
            File.Delete(outputPath);
            return outputPath;
        }
        catch (IOException)
        {
            string directory = Path.GetDirectoryName(outputPath);
            string fileName = Path.GetFileNameWithoutExtension(outputPath);
            string extension = Path.GetExtension(outputPath);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fallbackPath = Path.Combine(directory, fileName + "_" + timestamp + extension);
            Debug.LogWarning("asset_info.xlsx is open or locked. Wrote a timestamped copy instead: " + fallbackPath);
            return fallbackPath;
        }
        catch (System.UnauthorizedAccessException)
        {
            string directory = Path.GetDirectoryName(outputPath);
            string fileName = Path.GetFileNameWithoutExtension(outputPath);
            string extension = Path.GetExtension(outputPath);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fallbackPath = Path.Combine(directory, fileName + "_" + timestamp + extension);
            Debug.LogWarning("asset_info.xlsx cannot be overwritten. Wrote a timestamped copy instead: " + fallbackPath);
            return fallbackPath;
        }
    }

    private static List<string[]> BuildResourceInfoRows(GeneratedAsset asset)
    {
        var rows = new List<string[]>();
        rows.Add(new[] { "航空航天素材资源信息表｜" + asset.AssetName + " v001", "", "", "" });
        rows.Add(new[] { "黄色单元格为制作人必须填写或核对的字段；未取得证据的项目明确标注为待补充/待测试。", "", "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "一、资源身份", "", "", "" });
        rows.Add(new[] { "asset_id", asset.AssetName.ToLowerInvariant(), "", "" });
        rows.Add(new[] { "中文标准名", asset.AssetName + " AR 教学模型", "", "" });
        rows.Add(new[] { "英文名", asset.AssetName + " AR asset", "", "" });
        rows.Add(new[] { "版本", "v001", "", "" });
        rows.Add(new[] { "分类", "航空航天 / 飞机 / 三维模型", "", "" });
        rows.Add(new[] { "标签", "aerospace; aircraft; AR; VR; education", "", "" });
        rows.Add(new[] { "制作人 / 指导老师", "待填写", "", "" });
        rows.Add(new[] { "技术审核 / 内容审核", "待填写", "", "" });
        rows.Add(new[] { "提交日期", System.DateTime.Now.ToString("yyyy-MM-dd"), "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "二、制作与运行环境", "", "", "" });
        rows.Add(new[] { "DCC 软件与版本", "FBX 已导入 Unity；原 DCC 软件与版本待制作方补充", "", "" });
        rows.Add(new[] { "Unity 版本", Application.unityVersion, "", "" });
        rows.Add(new[] { "渲染管线", "Built-in", "", "" });
        rows.Add(new[] { "PICO SDK / XR 插件", "当前工程未检测到专用 PICO SDK；加载端/插件版本待集成方确认", "", "" });
        rows.Add(new[] { "目标设备", "Android / PICO / 移动 AR 端，具体机型待项目指定", "", "" });
        rows.Add(new[] { "目标刷新率", "待真机测试；建议按 72 Hz 或项目目标刷新率验收", "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "三、模型与资源统计", "", "", "" });
        rows.Add(new[] { "指标", "LOD0", "LOD1", "LOD2" });
        rows.Add(new[] { "面数", asset.Stats.TriangleCount.ToString(), "待补充", "待补充" });
        rows.Add(new[] { "顶点", asset.Stats.VertexCount.ToString(), "待补充", "待补充" });
        rows.Add(new[] { "Renderer", asset.Stats.RendererCount.ToString(), "", "" });
        rows.Add(new[] { "材质", asset.Stats.MaterialCount.ToString(), "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "贴图数量 / 最大尺寸", asset.Stats.TextureSummary, "", "" });
        rows.Add(new[] { "Android 纹理压缩", "沿用 Unity 当前平台导入设置；如文字不清晰，优先检查贴图嵌入与原图分辨率", "", "" });
        rows.Add(new[] { "动画数量", "0 / 未检测到动画；如有机械演示需另行补充", "", "" });
        rows.Add(new[] { "Collider 类型与数量", "未自动生成 Collider；如需交互选取，建议补 Box/Capsule/低模 MeshCollider", "", "" });
        rows.Add(new[] { "Prefab 路径", asset.PrefabPath, "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "四、版权、考据与已知问题", "", "", "" });
        rows.Add(new[] { "模型原创范围", "当前可确认：用户提供源模型并由工具生成 Unity prefab、材质副本与 AB；具体原创建模范围需制作方书面确认。", "", "" });
        rows.Add(new[] { "第三方内容", "包含用户提供的 FBX、贴图和材质；工程中未见独立授权文件，需补充作者、来源、授权协议或购买凭证。", "", "" });
        rows.Add(new[] { "授权范围", "待补充正式授权。建议先限定为高校教学/AR 展示项目内部使用；未获授权前不建议商用或公开二次分发。", "", "" });
        rows.Add(new[] { "关键参考", "参考来源需由制作方补充到 06_docs/references_copyright.md；当前表格不伪造版权来源。", "", "" });
        rows.Add(new[] { "已知问题", "已完成：FBX 导入、材质参数调整、SafeZone 尺寸归一、Prefab/AB/UnityPackage 流程。待补充：DCC 源文件、LOD、正式版权证明、真机性能记录、预览图/视频归档。", "", "" });
        return rows;
    }

    private static List<string[]> BuildAcceptanceRows(GeneratedAsset asset)
    {
        var rows = new List<string[]>();
        rows.Add(new[] { "PICO / Unity 性能验收记录", "", "", "", "" });
        rows.Add(new[] { "本页数值需来自目标设备真机；项目预算是起始门槛，最终以真实教学场景为准。", "", "", "", "" });
        rows.Add(BlankRow(5));
        rows.Add(new[] { "指标", "项目目标", "实测结果", "状态", "证据/备注" });
        rows.Add(new[] { "测试设备", "填写机型与系统版本", "待填写目标设备型号与系统版本", "待测试", "需在目标 Android/PICO/移动 AR 设备上记录" });
        rows.Add(new[] { "Unity / PICO SDK", "填写完整版本号", Application.unityVersion + " / Built-in", "已核对", "工程按 Built-in 处理" });
        rows.Add(new[] { "平均/最低帧率", "稳定达到项目刷新率；默认基线 72 FPS", "待真机测试", "待测试", "需记录平均/最低 FPS 与测试场景" });
        rows.Add(new[] { "可见面数", "常规教学场景建议 <= 300k", asset.Stats.TriangleCount.ToString(), "待复核", "由生成 prefab 统计，仍建议用 Unity/Profiler 复核" });
        rows.Add(new[] { "Draw Calls", "建议 <= 150", "待 Profiler 统计", "待测试", "材质 " + asset.Stats.MaterialCount + " 个，实际 Draw Calls 受合批/实例化影响" });
        rows.Add(new[] { "SetPass Calls", "建议 <= 100", "待 Profiler 统计", "待测试", "需真机或目标运行端 Profile" });
        rows.Add(new[] { "实时主方向光", "通常 <= 1", "Prefab 不强绑场景灯光", "通过", "运行端照明由场景控制" });
        rows.Add(new[] { "纹理", "常规单张 <= 2048；Android/PICO 优先 ASTC", asset.Stats.TextureSummary, "需复核", "如包体压力较大，可再做平台压缩策略" });
        rows.Add(new[] { "首次加载", "无明显长时间卡顿", "待测试", "待测试", "需记录首次加载耗时" });
        rows.Add(new[] { "测试时长/场景", "记录时长与同时可见资产", "待填写", "待测试", "建议记录测试时长、同时可见素材数量、截图/视频证据" });
        rows.Add(BlankRow(5));
        rows.Add(new[] { "完成度", "35%", "", "", "" });
        return rows;
    }

    private static List<string[]> BuildFileChecklistRows(GeneratedAsset asset)
    {
        var rows = new List<string[]>();
        rows.Add(new[] { "标准提交文件清单", "", "", "", "" });
        rows.Add(BlankRow(5));
        rows.Add(new[] { "序号", "目录/文件", "必需性", "状态", "说明" });
        rows.Add(new[] { "1", "01_source/DCC/<source>", "必须", "缺失", "未发现可编辑 DCC 源文件；需制作方补充" });
        rows.Add(new[] { "2", "01_source/Model/" + Path.GetFileName(asset.SourcePath), "必须", "已放置", "原始资源：" + asset.SourcePath });
        rows.Add(new[] { "3", "01_source/Model/" + asset.AssetName + "_lod1.fbx", "建议", "缺失", "正式提交建议补齐 LOD1" });
        rows.Add(new[] { "4", "01_source/Model/" + asset.AssetName + "_lod2.fbx", "建议", "缺失", "正式提交建议补齐 LOD2" });
        rows.Add(new[] { "5", "01_source/Textures/<source textures>", "必须", "需复核", "贴图数量/尺寸见资源信息页；来源授权需补充" });
        rows.Add(new[] { "6", "Assets/Art/" + asset.AssetName + "/Material/*.mat", "必须", "已生成", "保留制作方在 Unity 中调整后的材质；工具不再强制修改 Emission/Metallic/Smoothness" });
        rows.Add(new[] { "7", "02_unity/" + asset.AssetName + ".unitypackage", "必须", "已生成", "需在空工程回归导入验证" });
        rows.Add(new[] { "8", "03_assetbundles/Android/" + asset.BundleFileName, "必须", "已生成", "需确认文件为 MB 级且 manifest 包含 MeshRenderer/Texture 等类型，避免旧 1KB 空包" });
        rows.Add(new[] { "9", "03_assetbundles/iOS/" + asset.BundleFileName, "按需", "已生成", "按需提交；需目标平台验证" });
        rows.Add(new[] { "10", "04_Images/preview_*.png 或 jpg", "必须", "待补充", "建议补正面、侧面、俯视、细节、AR 真机预览" });
        rows.Add(new[] { "11", "05_video/demo_v001.mp4", "必须", "待补充", "建议补真机或 Unity 预览视频" });
        rows.Add(new[] { "12", "06_docs/asset_info.xlsx", "必须", "已生成", "本工作簿" });
        rows.Add(new[] { "13", "06_docs/CHANGELOG.md", "必须", "待补充", "建议记录每次打包、材质、贴图、位置调整" });
        rows.Add(new[] { "14", "06_docs/references_copyright.md", "必须", "待补充", "需补正式模型/贴图授权文件与参考来源清单" });
        rows.Add(new[] { "15", "06_docs/source_mapping.md", "必须", "待补充", "建议记录原始文件名、规范文件名、导入路径、AB 名称" });
        rows.Add(new[] { "16", "06_docs/acceptance_checklist.md", "必须", "待测试", "需真机性能数据、截图/视频证据和签字确认" });
        rows.Add(BlankRow(5));
        rows.Add(new[] { "必需项完成度", "50%", "", "", "" });
        return rows;
    }

    private static string[] BlankRow(int cols)
    {
        var row = new string[cols];
        for (int i = 0; i < cols; i++)
        {
            row[i] = "";
        }

        return row;
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using (Stream stream = entry.Open())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(content);
        }
    }

    private static string BuildSheetXml(List<string[]> rows, int columnCount)
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        xml.Append("<sheetViews><sheetView showGridLines=\"0\" workbookViewId=\"0\"/></sheetViews>");
        xml.Append("<cols>");
        xml.Append("<col min=\"1\" max=\"1\" width=\"24\" customWidth=\"1\"/>");
        xml.Append("<col min=\"2\" max=\"").Append(columnCount).Append("\" width=\"24\" customWidth=\"1\"/>");
        xml.Append("</cols><sheetData>");

        var merges = new List<string>();
        for (int r = 0; r < rows.Count; r++)
        {
            string[] row = rows[r];
            bool titleRow = r == 0;
            bool noteRow = r == 1;
            bool sectionRow = IsSectionRow(row);
            bool headerRow = IsHeaderRow(row);
            int rowHeight = titleRow ? 28 : sectionRow ? 23 : noteRow ? 24 : 20;
            xml.Append("<row r=\"").Append(r + 1).Append("\" ht=\"").Append(rowHeight).Append("\" customHeight=\"1\">");
            for (int c = 0; c < row.Length; c++)
            {
                string value = row[c] ?? "";
                if (value.Length == 0)
                {
                    continue;
                }

                int style = GetCellStyle(row, r, c, titleRow, noteRow, sectionRow, headerRow);
                xml.Append("<c r=\"").Append(ColumnName(c + 1)).Append(r + 1).Append("\" t=\"inlineStr\" s=\"").Append(style).Append("\"><is><t>");
                xml.Append(XmlEscape(value));
                xml.Append("</t></is></c>");
            }
            xml.Append("</row>");

            if ((titleRow || noteRow || sectionRow) && columnCount > 1)
            {
                merges.Add("A" + (r + 1) + ":" + ColumnName(columnCount) + (r + 1));
            }
            else if (ShouldMergeValueRow(row) && columnCount > 2)
            {
                merges.Add("B" + (r + 1) + ":" + ColumnName(columnCount) + (r + 1));
            }
        }

        xml.Append("</sheetData>");
        if (merges.Count > 0)
        {
            xml.Append("<mergeCells count=\"").Append(merges.Count).Append("\">");
            foreach (string merge in merges)
            {
                xml.Append("<mergeCell ref=\"").Append(merge).Append("\"/>");
            }
            xml.Append("</mergeCells>");
        }

        xml.Append("</worksheet>");
        return xml.ToString();
    }

    private static bool IsSectionRow(string[] row)
    {
        return row.Length > 0 && (row[0].StartsWith("一、") || row[0].StartsWith("二、") || row[0].StartsWith("三、") || row[0].StartsWith("四、"));
    }

    private static bool IsHeaderRow(string[] row)
    {
        return row.Length > 1 && (row[0] == "指标" || row[0] == "序号");
    }

    private static bool ShouldMergeValueRow(string[] row)
    {
        if (row.Length < 3 || string.IsNullOrEmpty(row[0]) || string.IsNullOrEmpty(row[1]))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(row[2]))
        {
            return false;
        }

        return row[0] != "指标" && row[0] != "序号";
    }

    private static int GetCellStyle(string[] row, int rowIndex, int columnIndex, bool titleRow, bool noteRow, bool sectionRow, bool headerRow)
    {
        if (titleRow)
        {
            return 1;
        }

        if (sectionRow)
        {
            return 2;
        }

        if (noteRow)
        {
            return 3;
        }

        if (headerRow)
        {
            return 4;
        }

        if (columnIndex == 0)
        {
            return 5;
        }

        return 3;
    }

    private static string ColumnName(int index)
    {
        var name = "";
        while (index > 0)
        {
            int rem = (index - 1) % 26;
            name = (char)('A' + rem) + name;
            index = (index - rem - 1) / 26;
        }

        return name;
    }

    private static string BuildContentTypesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
            "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>" +
            "</Types>";
    }

    private static string BuildRootRelsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
            "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
            "</Relationships>";
    }

    private static string BuildWorkbookXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets>" +
            "<sheet name=\"资源信息\" sheetId=\"1\" r:id=\"rId1\"/>" +
            "<sheet name=\"性能验收\" sheetId=\"2\" r:id=\"rId2\"/>" +
            "<sheet name=\"文件清单\" sheetId=\"3\" r:id=\"rId3\"/>" +
            "</sheets></workbook>";
    }

    private static string BuildWorkbookRelsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
            "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet3.xml\"/>" +
            "<Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";
    }

    private static string BuildStylesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"16\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"5\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF16486B\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE9EFF5\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF2CC\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"2\"><border/><border><left style=\"thin\"><color rgb=\"FFD9E2EC\"/></left><right style=\"thin\"><color rgb=\"FFD9E2EC\"/></right><top style=\"thin\"><color rgb=\"FFD9E2EC\"/></top><bottom style=\"thin\"><color rgb=\"FFD9E2EC\"/></bottom></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"6\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf></cellXfs>" +
            "</styleSheet>";
    }

    private static string BuildCorePropsXml()
    {
        string now = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            "<dc:creator>Retinar Batch Builder</dc:creator><cp:lastModifiedBy>Retinar Batch Builder</cp:lastModifiedBy><dcterms:created xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:modified></cp:coreProperties>";
    }

    private static string BuildAppPropsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>Unity Retinar Batch Builder</Application></Properties>";
    }

    private static string XmlEscape(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}
