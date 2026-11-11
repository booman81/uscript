// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptPlayModeGuard.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Makes sure Play runs the graph code you just saved. When Play is pressed while that code is still compiling
//   (or hasn't been compiled yet), Play is held back and started automatically as soon as Unity has finished.
//   This matters most with "Enter Play Mode Options" (no domain reload), where Unity can otherwise start the game
//   with the previous code.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;

using Detox.Editor;

using UnityEditor;
using UnityEditor.Compilation;

using UnityEngine;

[InitializeOnLoad]
public static class uScriptPlayModeGuard
{
   private const string AwaitingCompileKey = "uScript.CodeAwaitingCompile";
   private const string AwaitingSinceKey = "uScript.CodeAwaitingCompileSince";
   private const string PlayAfterCompileKey = "uScript.PlayAfterCompile";
   private const string PlayRequestedAtKey = "uScript.PlayRequestedAt";
   private const string CompileStartedKey = "uScript.PlayGuardCompileStarted";
   private const string LastReloadKey = "uScript.LastAssemblyReloadUtcTicks";

   // If Unity doesn't start compiling within this many seconds after Play was held back, play anyway
   private const float StartTimeout = 4f;

   static uScriptPlayModeGuard()
   {
      // This runs after every script reload: generated code written after this moment isn't loaded yet
      SessionState.SetString(LastReloadKey, DateTime.UtcNow.Ticks.ToString());

      EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

      AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
      AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

      CompilationPipeline.compilationStarted -= OnCompilationStarted;
      CompilationPipeline.compilationStarted += OnCompilationStarted;

      CompilationPipeline.compilationFinished -= OnCompilationFinished;
      CompilationPipeline.compilationFinished += OnCompilationFinished;

      EditorApplication.update -= OnUpdate;
      EditorApplication.update += OnUpdate;
   }

   /// <summary>Called after uScript wrote generated scripts: they still have to be compiled.</summary>
   public static void MarkCodeChanged()
   {
      SessionState.SetBool(AwaitingCompileKey, true);
      SessionState.SetFloat(AwaitingSinceKey, (float)EditorApplication.timeSinceStartup);
   }

   /// <summary>
   /// Starts Play as soon as the code uScript just saved is compiled and loaded (right away when nothing has to
   /// be compiled). Used by "Save &amp; Play".
   /// </summary>
   public static void PlayWhenReady()
   {
      if (EditorApplication.isPlayingOrWillChangePlaymode)
      {
         return;
      }

      if (CodeAwaitingCompile || EditorApplication.isCompiling)
      {
         SessionState.SetBool(PlayAfterCompileKey, true);
         SessionState.SetFloat(PlayRequestedAtKey, (float)EditorApplication.timeSinceStartup);
         SessionState.SetBool(CompileStartedKey, EditorApplication.isCompiling);
         Debug.Log("uScript: Play starts when the saved graph code has finished compiling.");
         return;
      }

      EditorApplication.delayCall += () =>
      {
         if (!EditorApplication.isPlayingOrWillChangePlaymode)
         {
            EditorApplication.isPlaying = true;
         }
      };
   }

   private static bool CodeAwaitingCompile
   {
      get { return SessionState.GetBool(AwaitingCompileKey, false); }
   }

   private static void OnPlayModeStateChanged(PlayModeStateChange state)
   {
      if (state != PlayModeStateChange.ExitingEditMode)
      {
         return;
      }

      var newerFiles = GeneratedScriptsNewerThanLastReload();

      if (!CodeAwaitingCompile && !EditorApplication.isCompiling && newerFiles.Length == 0)
      {
         return;
      }

      // Make sure Unity picks up generated scripts written since the last reload, however they were saved
      foreach (var file in newerFiles)
      {
         var assetPath = file.RelativeAssetPath();
         if (assetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
         {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
         }
      }

      // Hold Play until the new graph code is compiled and loaded
      EditorApplication.isPlaying = false;
      SessionState.SetBool(PlayAfterCompileKey, true);
      SessionState.SetFloat(PlayRequestedAtKey, (float)EditorApplication.timeSinceStartup);
      SessionState.SetBool(CompileStartedKey, EditorApplication.isCompiling);

      Debug.Log("uScript: waiting for the saved graph code to finish compiling. Play starts automatically.");

      if (!EditorApplication.isCompiling)
      {
         AssetDatabase.Refresh();
         CompilationPipeline.RequestScriptCompilation();
      }
   }

   private static void OnCompilationStarted(object context)
   {
      SessionState.SetBool(CompileStartedKey, true);
   }

   /// <summary>Generated graph scripts changed on disk since the scripts were last loaded.</summary>
   private static string[] GeneratedScriptsNewerThanLastReload()
   {
      try
      {
         long ticks;
         if (!long.TryParse(SessionState.GetString(LastReloadKey, string.Empty), out ticks))
         {
            return new string[0];
         }

         var lastReload = new DateTime(ticks, DateTimeKind.Utc);

         return new[] { Preferences.GeneratedScripts, Preferences.NestedScripts }
            .Where(folder => !string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            .Select(folder => folder.Replace('\\', '/').TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(folder => Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => File.GetLastWriteTimeUtc(file) > lastReload)
            .Select(file => file.Replace('\\', '/'))
            .ToArray();
      }
      catch (Exception)
      {
         return new string[0];
      }
   }

   private static void OnAfterAssemblyReload()
   {
      SessionState.EraseBool(AwaitingCompileKey);
      StartPlayIfRequested();
   }

   private static void OnCompilationFinished(object context)
   {
      if (!EditorUtility.scriptCompilationFailed)
      {
         return;   // the assembly reload follows and starts Play
      }

      // Compile errors: no reload will come, so don't wait any longer
      SessionState.EraseBool(AwaitingCompileKey);
      if (SessionState.GetBool(PlayAfterCompileKey, false))
      {
         SessionState.EraseBool(PlayAfterCompileKey);
         SessionState.EraseBool(CompileStartedKey);
         Debug.LogWarning("uScript: Play was not started because the scripts have compile errors.");
      }
   }

   private static void OnUpdate()
   {
      if (!SessionState.GetBool(PlayAfterCompileKey, false) || EditorApplication.isCompiling || EditorApplication.isUpdating)
      {
         return;
      }

      // Once compiling has started, the assembly reload (or the compile errors) decide
      if (SessionState.GetBool(CompileStartedKey, false))
      {
         return;
      }

      // Nothing started compiling after all (e.g. the code was already up to date): play now
      var requestedAt = SessionState.GetFloat(PlayRequestedAtKey, 0f);
      if (EditorApplication.timeSinceStartup - requestedAt > StartTimeout)
      {
         SessionState.EraseBool(AwaitingCompileKey);
         StartPlayIfRequested();
      }
   }

   private static void StartPlayIfRequested()
   {
      if (!SessionState.GetBool(PlayAfterCompileKey, false))
      {
         return;
      }

      SessionState.EraseBool(PlayAfterCompileKey);
      SessionState.EraseBool(CompileStartedKey);

      EditorApplication.delayCall += () =>
      {
         if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorUtility.scriptCompilationFailed)
         {
            EditorApplication.isPlaying = true;
         }
      };
   }
}
