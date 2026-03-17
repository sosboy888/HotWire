// ImportManualPDF.cs
// Menu: Tools → KTANE → Import Manual PDF Pages
//
// Converts bomb_defusal_manual_hotwire.pdf (project root) into a series of
// PNG textures named page_000.png … page_NNN.png and saves them to
// Assets/Resources/ManualPages/
//
// Requires ONE of the following tools to be installed and accessible:
//   • Ghostscript   ghostscript.com              (gswin64c / gswin32c)
//   • Poppler       github.com/oschwartz10612/poppler-windows  (pdftoppm)
//   • ImageMagick   imagemagick.org              (magick)
//
// If none are found the script prints install instructions and exits.
// After the import the ManualTablet will automatically switch to PDF mode
// on the next Play.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ImportManualPDF
{
    private const string PdfName       = "bomb_defusal_manual_hotwire.pdf";
    private const string ResourcesPath = "Assets/Resources";
    private const string PagesFolder   = "Assets/Resources/ManualPages";

    // ── Entry point ───────────────────────────────────────────────────────────
    [MenuItem("Tools/KTANE/Import Manual PDF Pages")]
    public static void Run()
    {
        // ── 1. Locate PDF ─────────────────────────────────────────────────────
        string pdfFull = Path.GetFullPath(PdfName);
        if (!File.Exists(pdfFull))
        {
            EditorUtility.DisplayDialog("PDF Not Found",
                $"Could not find:\n{pdfFull}\n\n" +
                "Place the PDF at the project root (same folder as Assets/).",
                "OK");
            return;
        }

        // ── 2. Ensure output folders exist ────────────────────────────────────
        if (!AssetDatabase.IsValidFolder(ResourcesPath))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PagesFolder))
            AssetDatabase.CreateFolder(ResourcesPath, "ManualPages");

        string outDir = Path.GetFullPath(PagesFolder);

        // ── 3. Find a conversion tool ─────────────────────────────────────────
        (string toolType, string toolExe) = FindTool();

        if (toolExe == null)
        {
            EditorUtility.DisplayDialog("No PDF Converter Found",
                "No PDF conversion tool found on this machine.\n\n" +
                "Please install ONE of the following and restart Unity:\n\n" +
                "  • Ghostscript  —  ghostscript.com\n" +
                "    (adds gswin64c.exe to PATH)\n\n" +
                "  • Poppler for Windows\n" +
                "    github.com/oschwartz10612/poppler-windows\n" +
                "    (adds pdftoppm.exe to PATH or C:\\poppler\\bin)\n\n" +
                "  • ImageMagick  —  imagemagick.org\n" +
                "    (adds magick.exe to PATH)\n\n" +
                "─────────────────────────────────────────\n\n" +
                "ALTERNATIVE — manual conversion:\n" +
                "  Convert each PDF page to a PNG file yourself,\n" +
                $"  name them page_000.png, page_001.png …\n" +
                $"  and place them in:\n  {PagesFolder}/",
                "OK");
            return;
        }

        UnityEngine.Debug.Log($"[ImportManualPDF] Using {toolType}: {toolExe}");

        // ── 4. Convert PDF pages → PNG ────────────────────────────────────────
        bool ok = ConvertPDF(toolType, toolExe, pdfFull, outDir);
        if (!ok) return;

        // ── 5. Import and configure textures ─────────────────────────────────
        AssetDatabase.Refresh();

        int count = 0;
        for (int i = 0; i < 500; i++)
        {
            string assetPath = $"{PagesFolder}/page_{i:D3}.png";
            if (!File.Exists(Path.GetFullPath(assetPath))) break;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType    = TextureImporterType.Default;
                importer.isReadable     = false;
                importer.mipmapEnabled  = false;
                importer.filterMode     = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            count++;
        }

        if (count == 0)
        {
            EditorUtility.DisplayDialog("Import Problem",
                $"The converter ran but no page_NNN.png files were found in:\n{PagesFolder}/\n\n" +
                "Check the Unity Console for tool output.", "OK");
            return;
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Import Complete",
            $"Imported {count} PDF page{(count == 1 ? "" : "s")} into:\n{PagesFolder}/\n\n" +
            "The ManualTablet will automatically switch to PDF display mode " +
            "the next time you enter Play mode.",
            "OK");
    }

    // ── Tool discovery ────────────────────────────────────────────────────────

    private static (string type, string exe) FindTool()
    {
        // pdftoppm (Poppler) — preferred; outputs one PNG per page cleanly
        string pdftoppm = FindExe("pdftoppm",
            @"C:\poppler\bin",
            @"C:\Program Files\poppler\bin",
            @"C:\Program Files (x86)\poppler\bin");
        if (pdftoppm != null) return ("pdftoppm", pdftoppm);

        // Ghostscript
        foreach (string gsName in new[] { "gswin64c", "gswin32c", "gs" })
        {
            string gs = FindGhostscript(gsName);
            if (gs != null) return ("ghostscript", gs);
        }

        // ImageMagick
        string magick = FindExe("magick",
            @"C:\Program Files\ImageMagick-7.1.0-Q16-HDRI",
            @"C:\Program Files\ImageMagick-7.0.0-Q16");
        if (magick != null) return ("imagemagick", magick);

        return (null, null);
    }

    private static string FindExe(string name, params string[] extraDirs)
    {
        // Search PATH environment variable
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in pathEnv.Split(';'))
        {
            string full = Path.Combine(dir.Trim(), name + ".exe");
            if (File.Exists(full)) return full;
        }

        // Search extra well-known directories
        foreach (string dir in extraDirs)
        {
            string full = Path.Combine(dir, name + ".exe");
            if (File.Exists(full)) return full;
        }

        return null;
    }

    private static string FindGhostscript(string exeName)
    {
        // Try PATH first
        string found = FindExe(exeName);
        if (found != null) return found;

        // Ghostscript installs under C:\Program Files\gs\gsX.YY.Z\bin\
        foreach (string programFiles in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        })
        {
            string gsRoot = Path.Combine(programFiles, "gs");
            if (!Directory.Exists(gsRoot)) continue;
            foreach (string ver in Directory.GetDirectories(gsRoot))
            {
                string exe = Path.Combine(ver, "bin", exeName + ".exe");
                if (File.Exists(exe)) return exe;
            }
        }

        return null;
    }

    // ── Conversion ────────────────────────────────────────────────────────────

    private static bool ConvertPDF(string toolType, string toolExe,
                                   string pdfPath,  string outDir)
    {
        string args;
        string tmpPrefix = null; // only used for pdftoppm renaming step

        switch (toolType)
        {
            case "pdftoppm":
                // pdftoppm outputs:  prefix-001.png, prefix-002.png …
                tmpPrefix = Path.Combine(outDir, "pdftmp");
                args = $"-r 150 -png \"{pdfPath}\" \"{tmpPrefix}\"";
                break;

            case "ghostscript":
                // gswin64c outputs:  page_001.png, page_002.png … (1-indexed)
                args = $"-dNOPAUSE -dBATCH -sDEVICE=png16m -r150 " +
                       $"-sOutputFile=\"{Path.Combine(outDir, "page_%03d.png")}\" " +
                       $"\"{pdfPath}\"";
                break;

            case "imagemagick":
                // magick outputs:  page_000.png, page_001.png … (0-indexed) when format is %03d
                args = $"-density 150 \"{pdfPath}\" " +
                       $"\"{Path.Combine(outDir, "page_%03d.png")}\"";
                break;

            default:
                return false;
        }

        // Run the tool
        var psi = new ProcessStartInfo(toolExe, args)
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };

        try
        {
            using var proc = Process.Start(psi);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(120_000); // 2 min max

            if (!string.IsNullOrEmpty(stdout))
                UnityEngine.Debug.Log($"[ImportManualPDF] {toolType} stdout:\n{stdout}");
            if (!string.IsNullOrEmpty(stderr))
                UnityEngine.Debug.Log($"[ImportManualPDF] {toolType} stderr:\n{stderr}");

            if (proc.ExitCode != 0)
            {
                EditorUtility.DisplayDialog("Conversion Error",
                    $"{toolType} returned exit code {proc.ExitCode}.\n\n" +
                    "Check the Unity Console for details.", "OK");
                return false;
            }
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Conversion Error", ex.Message, "OK");
            return false;
        }

        // ── Rename pdftoppm output to page_000.png … ─────────────────────────
        if (toolType == "pdftoppm" && tmpPrefix != null)
        {
            // pdftoppm produces:  pdftmp-001.png, pdftmp-002.png …
            var produced = new List<string>(
                Directory.GetFiles(outDir, "pdftmp-*.png"));
            produced.Sort(StringComparer.Ordinal);

            for (int i = 0; i < produced.Count; i++)
            {
                string dest = Path.Combine(outDir, $"page_{i:D3}.png");
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(produced[i], dest);
            }
        }

        // ── Ghostscript outputs page_001.png … — shift to page_000.png … ─────
        if (toolType == "ghostscript")
        {
            // Find all page_NNN.png, sort descending, shift indices by -1
            var produced = new List<string>(
                Directory.GetFiles(outDir, "page_*.png"));
            produced.Sort(StringComparer.OrdinalIgnoreCase);
            // Rename in reverse to avoid collisions: page_002→page_001, page_001→page_000
            for (int i = produced.Count - 1; i >= 0; i--)
            {
                // Extract number from filename
                string fn  = Path.GetFileNameWithoutExtension(produced[i]); // "page_001"
                if (!int.TryParse(fn.Replace("page_", ""), out int num)) continue;
                string dest = Path.Combine(outDir, $"page_{num - 1:D3}.png");
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(produced[i], dest);
            }
        }

        return true;
    }
}
