using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CyberBiker.UI {
    // Versioned, portable JSON; no platform file permissions are needed for the
    // local backup. Modern Android additionally publishes to Downloads.
    public static class BalancePresetIO {
        public const string Format="CyberBikerBalance";
        public const int SchemaVersion=1,MaximumCharacters=131072;
        [Serializable]private sealed class Envelope {public string format;public int schemaVersion;public string exportedUtc;public TestingSettings settings;}
        public static string ExportJson(TestingSettings draft){
            if(draft==null)throw new ArgumentNullException(nameof(draft));
            var copy=draft.Copy();copy.Normalize();
            return JsonUtility.ToJson(new Envelope{format=Format,schemaVersion=SchemaVersion,exportedUtc=DateTime.UtcNow.ToString("o"),settings=copy},true);
        }
        public static bool TryImport(string json,TestingSettings current,out TestingSettings imported,out string error){
            imported=null;error=null;
            if(string.IsNullOrWhiteSpace(json)){error="Paste a preset first.";return false;}
            if(json.Length>MaximumCharacters){error="Preset is too large.";return false;}
            json=json.Trim().TrimStart('\uFEFF');
            try{
                var header=JsonUtility.FromJson<Envelope>(json);
                if(header==null || header.format!=Format || header.schemaVersion!=SchemaVersion || header.settings==null){error="This is not a supported CyberBiker balance preset.";return false;}
                var merged=new Envelope{format=Format,schemaVersion=SchemaVersion,settings=current.Copy()};
                JsonUtility.FromJsonOverwrite(json,merged);
                if(merged.settings==null || merged.settings.scoreBalance==null || merged.settings.triggerBalance==null){error="Preset settings are missing.";return false;}
                merged.settings.Normalize();imported=merged.settings;return true;
            }catch(ArgumentException){error="The preset JSON could not be read.";return false;}
        }
        public static string ExportFolder {
            get {
#if UNITY_EDITOR
                return Path.Combine(Directory.GetParent(Application.dataPath).FullName,"BalancePresets");
#else
                return Path.Combine(Application.persistentDataPath,"BalancePresets");
#endif
            }
        }
        public static string ExportFile(string json){
            string name="CyberBiker-balance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"Z-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json";
            Directory.CreateDirectory(ExportFolder);string local=Path.Combine(ExportFolder,name);File.WriteAllText(local,json,new UTF8Encoding(false));
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var version=new AndroidJavaClass("android.os.Build$VERSION")){
                if(version.GetStatic<int>("SDK_INT")>=29)return ExportAndroidDownloads(name,json);
            }
            using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))using(var folder=activity.Call<AndroidJavaObject>("getExternalFilesDir","")){
                if(folder==null)throw new IOException("External storage is unavailable. A local app backup was saved.");
                string external=Path.Combine(folder.Call<string>("getAbsolutePath"),"BalancePresets");Directory.CreateDirectory(external);string path=Path.Combine(external,name);File.WriteAllText(path,json,new UTF8Encoding(false));return path;
            }
#else
            return local;
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private static string ExportAndroidDownloads(string name,string json){
            using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))using(var resolver=activity.Call<AndroidJavaObject>("getContentResolver"))using(var downloads=new AndroidJavaClass("android.provider.MediaStore$Downloads"))using(var collection=downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))using(var values=new AndroidJavaObject("android.content.ContentValues"))using(var one=new AndroidJavaObject("java.lang.Integer",1))using(var zero=new AndroidJavaObject("java.lang.Integer",0)){
                values.Call("put","_display_name",name);values.Call("put","mime_type","application/json");values.Call("put","relative_path","Download/CyberBiker");values.Call("put","is_pending",one);
                using(var uri=resolver.Call<AndroidJavaObject>("insert",collection,values)){
                    if(uri==null)throw new IOException("Android could not create the Downloads file. A local backup was saved.");
                    try{
                        using(var stream=resolver.Call<AndroidJavaObject>("openOutputStream",uri)){
                            if(stream==null)throw new IOException("Android could not open the export file.");
                            byte[] bytes=Encoding.UTF8.GetBytes(json);var signed=new sbyte[bytes.Length];Buffer.BlockCopy(bytes,0,signed,0,bytes.Length);stream.Call("write",(object)signed);stream.Call("close");
                        }
                        values.Call("clear");values.Call("put","is_pending",zero);resolver.Call<int>("update",uri,values,"",new string[0]);
                    }catch{resolver.Call<int>("delete",uri,"",new string[0]);throw;}
                }
                return "Downloads/CyberBiker/"+name;
            }
        }
#endif
    }
}
