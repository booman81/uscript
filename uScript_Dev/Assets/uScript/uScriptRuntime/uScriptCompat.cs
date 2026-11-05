// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptCompat.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Helpers that hide Unity API differences between Unity versions (e.g. Unity 6 EntityId changes).
// </summary>
// --------------------------------------------------------------------------------------------------------------------

public static class uScriptCompat
{
   /// <summary>
   /// Returns an integer identifier for a UnityEngine.Object.
   /// Equivalent to Object.GetInstanceID(), which is deprecated in Unity 6.4 and a compile error from Unity 6.5.
   /// </summary>
   public static int GetObjectID(UnityEngine.Object obj)
   {
      if (obj == null)
      {
         return 0;
      }

#if UNITY_6000_4_OR_NEWER
      // Matches Unity's own implementation of the (now obsolete) GetInstanceID().
      return unchecked((int)(UnityEngine.EntityId.ToULong(obj.GetEntityId()) & 0xFFFFFFFF));
#else
      return obj.GetInstanceID();
#endif
   }
}
