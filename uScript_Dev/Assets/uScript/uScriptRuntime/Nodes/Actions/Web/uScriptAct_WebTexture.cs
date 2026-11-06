// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Detox Studios, LLC" file="uScriptAct_WebTexture.cs">
//   Copyright 2010-2013 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   uScript Action Node
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Networking;

[NodePath("Actions/Web/Download")]

[NodeCopyright("Copyright 2011 by Detox Studios LLC")]
[NodeToolTip("Download texture data from the specified URL.")]
[NodeAuthor("Detox Studios LLC", "http://www.detoxstudios.com")]
[NodeHelp("http://docs.uscript.net/#3-Working_With_uScript/3.4-Nodes.htm")]

[FriendlyName("Web Texture", "Download texture data (PNG or JPG) from the specified URL. If Form Data is given, the form is sent with a POST request." +
   "\n\nNote: newer Unity versions block plain http:// URLs by default. Use https://, or change" +
   " Project Settings > Player > Other Settings > Allow downloads over HTTP.")]
public class uScriptAct_WebTexture : uScriptLogic
{
   private UnityWebRequest www;
   private bool finished = true;
   private string startError;

   // ================================================================================
   //    Output Sockets
   // ================================================================================
   //
   [FriendlyName("Out")]
   public bool Out { get; private set; }

   [FriendlyName("Success")]
   public bool OutFinished { get; private set; }

   [FriendlyName("Error")]
   public bool OutError { get; private set; }

   // ================================================================================
   //    Input Sockets and Node Parameters
   // ================================================================================
   //
   public void In(
      [FriendlyName("URL", "The URL to download")]
      string URL,

      [FriendlyName("Form Data", "A WWWForm instance containing the form data to post."), SocketState(false, false)]
      WWWForm Form,

      [FriendlyName("Result", "The downloaded data.")]
      out Texture2D Result,

      [FriendlyName("Error", "Returns an error message if there was an error during the download."), SocketState(false, false)]
      out string Error)
   {
      this.Out = true;
      this.OutFinished = false;
      this.OutError = false;

      this.Cleanup();
      this.finished = false;
      this.startError = null;

      try
      {
         if (Form == null)
         {
            this.www = UnityWebRequestTexture.GetTexture(URL);
         }
         else
         {
            this.www = UnityWebRequest.Post(URL, Form);
            this.www.downloadHandler = new DownloadHandlerTexture(true);
         }

         this.www.SendWebRequest();
      }
      catch (System.Exception e)
      {
         // e.g. "Insecure connection not allowed" for http:// URLs, or an invalid URL
         this.startError = e.Message;
      }

      Result = null;
      Error = string.Empty;
   }

   // ================================================================================
   //    Miscellaneous Node Functionality
   // ================================================================================
   //
   [Driven]
   public bool Driven(out Texture2D result, out string error)
   {
      this.Out = false;

      result = null;
      error = string.Empty;

      if (this.finished)
      {
         return false;
      }

      if (this.startError != null)
      {
         this.finished = true;
         error = this.startError;
         this.OutError = true;
         this.Cleanup();
         return true;
      }

      if (this.www != null && this.www.isDone)
      {
         this.finished = true;

         if (!string.IsNullOrEmpty(this.www.error))
         {
            error = this.www.error;
            this.OutError = true;
         }
         else
         {
            var handler = this.www.downloadHandler as DownloadHandlerTexture;
            result = handler != null ? handler.texture : null;

            if (result == null)
            {
               error = "The downloaded data is not a valid PNG or JPG image.";
               this.OutError = true;
            }
            else
            {
               this.OutFinished = true;
            }
         }

         this.Cleanup();
      }

      return true;
   }

   private void Cleanup()
   {
      if (this.www != null)
      {
         this.www.Dispose();
         this.www = null;
      }
   }
}
