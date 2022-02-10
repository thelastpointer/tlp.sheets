using UnityEngine;
using UnityEditor;
using System.Linq;

namespace TLP.Sheets
{
    public class SheetList : ScriptableObject
    {
        public Sheet[] sheets;

        const string SheetListFile = "TLPSheets.asset";

        public static SheetList Get()
        {
            var list = EditorGUIUtility.Load(SheetListFile);

            if (list != null)
            {
                return list as SheetList;
            }

            return null;
        }

        public static void Create()
        {
            var sheetList = ScriptableObject.CreateInstance<SheetList>();
            sheetList.sheets = new Sheet[0];

            if (!AssetDatabase.IsValidFolder("Assets/Editor Default Resources"))
                AssetDatabase.CreateFolder("Assets", "Editor Default Resources");
            AssetDatabase.CreateAsset(sheetList, "Assets/Editor Default Resources/" + SheetListFile);
            AssetDatabase.SaveAssets();
        }

        public void Save()
        {
            EditorUtility.SetDirty(Get());
            AssetDatabase.SaveAssets();
        }

        public void DownloadAll(System.Action onFinished = null, System.Action<int, int> onProgress = null)
        {
            downloadFinishedCallback = onFinished;
            downloadProgressCallback = onProgress;
            downloadingIdx = 0;
            EditorApplication.update += ProgressUpdate;
        }

        public void Add(Sheet sheet)
        {
            sheets = sheets.Append(sheet).ToArray();
        }

        public void Remove(Sheet sheet)
        {
            if (sheets.Length == 0)
                return;

            int idx = -1;
            for (int i=0; i<sheets.Length; i++)
            {
                if (sheets[i].Equals(sheet))
                {
                    idx = i;
                    break;
                }
            }

            if (idx != -1)
            {
                var tmp = new Sheet[sheets.Length - 1];
                for (int i=0; i<idx; i++)
                {
                    tmp[i] = sheets[i];
                }

                for (int i=idx+1; i<sheets.Length; i++)
                {
                    tmp[i - 1] = sheets[i];
                }

                sheets = tmp;
            }
        }

        private int downloadingIdx = -1;
        private UnityEngine.Networking.UnityWebRequest currentRequest;
        private System.Action downloadFinishedCallback;
        private System.Action<int, int> downloadProgressCallback;

        private void ProgressUpdate()
        {
            // Wait for current request to complete
            if (currentRequest != null)
            {
                if (!currentRequest.isDone)
                {
                }
                else
                {
                    if (currentRequest.isHttpError)
                    {
                        Debug.LogError("Unable to download data (HTTP error): " + currentRequest.error);
                    }
                    else if (currentRequest.isNetworkError)
                    {
                        if (currentRequest.responseCode == 302)
                            Debug.LogError("Unable to download data. Are sheet permissions set correctly?\n" + currentRequest.error);
                        else
                            Debug.LogError("Unable to download data (network error): " + currentRequest.error);
                    }
                    else
                    {
                        try
                        {
                            string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath + "/../", sheets[downloadingIdx].LocalFilename));
                            System.IO.File.WriteAllText(fullPath, currentRequest.downloadHandler.text);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError("Unable to write to file \"" + sheets[downloadingIdx].LocalFilename + "\": " + e);
                        }
                    }

                    currentRequest = null;
                    downloadingIdx++;

                    downloadProgressCallback?.Invoke(downloadingIdx, sheets.Length);
                }
            }
            // No active request; move on to next sheet or terminate
            else
            {
                if (downloadingIdx < sheets.Length)
                {
                    currentRequest = UnityEngine.Networking.UnityWebRequest.Get(sheets[downloadingIdx].GetCSVURL());
                    currentRequest.redirectLimit = 1;
                    currentRequest.SendWebRequest();
                }
                else
                {
                    downloadingIdx = -1;
                    downloadFinishedCallback?.Invoke();

                    EditorApplication.update -= ProgressUpdate;

                    downloadFinishedCallback = null;
                    downloadProgressCallback = null;
                }
            }
        }

        [System.Serializable]
        public struct Sheet
        {
            public string SheetID;
            public string SheetGID;

            public string LocalFilename;

            public string Comment;

            public Object GetAsset()
            {
                return AssetDatabase.LoadMainAssetAtPath(LocalFilename);
            }

            public string GetURL()
            {
                return string.Format("https://docs.google.com/spreadsheets/d/{0}/edit#gid={1}", SheetID, SheetGID);
            }

            public string GetCSVURL()
            {
                //return string.Format("https://docs.google.com/spreadsheets/d/{0}/gviz/tq?tqx=out:csv&sheet={1}", SheetID, SheetGID);
                //return string.Format("https://docs.google.com/spreadsheets/d/e/{0}/pub?gid={1}&single=true&output=csv", SheetID, SheetGID);
                return string.Format("https://docs.google.com/spreadsheets/d/{0}/export?gid={1}&format=csv", SheetID, SheetGID);
            }

            public static Sheet Create(string id, string gid, string localFilename, string comment)
            {
                return new Sheet
                {
                    SheetID = id,
                    SheetGID = gid,
                    LocalFilename = localFilename,
                    Comment = comment
                };
            }

            public static Sheet FromURL(string url, string localFilename, string comment)
            {
                string id;
                string gid;

                if (!TryParseURL(url, out id, out gid))
                {
                    throw new System.ArgumentException("Not a valid Google sheet URL");
                }

                return new Sheet
                {
                    SheetID = id,
                    SheetGID = gid,
                    LocalFilename = localFilename,
                    Comment = comment
                };
            }

            public static bool TryParseURL(string url, out string id, out string gid)
            {
                id = null;
                gid = null;

                //https://docs.google.com/spreadsheets/d/**ID**/edit#gid=**GID**

                if (!url.StartsWith(UrlFirstPart, System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string usefulPart = url.Substring(UrlFirstPart.Length);

                // Get ID
                int idx = usefulPart.IndexOf("/");
                if (idx < 0)
                {
                    id = usefulPart;
                }
                else
                {
                    id = usefulPart.Substring(0, idx);
                }

                if (string.IsNullOrEmpty(id))
                {
                    return false;
                }

                // TODO: validate ID as [a-bA-B0-9]*

                // Find GID
                if (usefulPart.IndexOf("gid=") != -1)
                {
                    string gidPart = usefulPart.Substring(usefulPart.IndexOf("gid=") + 4);
                    idx = gidPart.IndexOf("/");
                    if (idx < 0)
                    {
                        gid = gidPart;
                    }
                    else
                    {
                        gid = gidPart.Substring(0, idx);
                    }
                }

                return true;
            }

            public bool Equals(Sheet other)
            {
                return
                    (string.CompareOrdinal(this.SheetID, other.SheetID) == 0) &&
                    (string.CompareOrdinal(this.SheetGID, other.SheetGID) == 0);
            }

            const string UrlFirstPart = "https://docs.google.com/spreadsheets/d/";
        }
    }
}