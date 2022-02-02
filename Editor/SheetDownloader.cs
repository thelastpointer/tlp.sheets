using UnityEngine;
using UnityEditor;

namespace TLP.Sheets
{
    public class SheetDownloader : EditorWindow
    {
        [MenuItem("Tools/Google sheet downloader...")]
        private static void ShowSheetsWindow()
        {
            var wnd = GetWindow<SheetDownloader>("Sheets");

            wnd.GetSheetList();

            wnd.Show();
        }

        private SheetList sheetList;
        private bool syncing = false;
        private Vector2 scroll;
        private int syncProgress, syncCount;

        #region GUI

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            GUILayout.Label("File list:");

            if (!syncing)
            {
                // Note: this might happen if the editor starts with the window open (ShowSheetsWindow isn't called in that case).
                if (sheetList == null)
                    GetSheetList();

                using (new EditorGUILayout.ScrollViewScope(scroll))
                {
                    foreach (var sheet in sheetList.sheets)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.TextField(sheet.Comment);
                            EditorGUILayout.ObjectField(sheet.GetAsset(), typeof(SerializedObject), false);
                            //EditorGUILayout.TextField(sheet.LocalFilename);
                            EditorGUILayout.TextField(sheet.SheetID + " / " + sheet.SheetGID);

                            if (GUILayout.Button("X"))
                            {
                                // Maybe ask if the file should be deleted as well?
                                //if (EditorUtility.DisplayDialog("Delete sheet", "Do you want to delete the downloaded file too?", "Yes", "No")) { }
                                sheetList.Remove(sheet);
                                break;
                            }
                        }
                    }
                }
                
                if (GUILayout.Button("Add New"))
                {
                    var wnd = GetWindow<NewSheetWindow>(true, "New Sheet", true);
                    wnd.OnAdd = (sheet) =>
                    {
                        sheetList.Add(sheet);
                        sheetList.Save();
                    };
                    wnd.ShowModalUtility();
                }

                if (GUILayout.Button("Download Now"))
                {
                    syncProgress = 0;
                    syncCount = 0;
                    syncing = true;

                    SheetList.Get().DownloadAll(
                        () =>
                        {
                            syncing = false;
                        },
                        (cur, count) =>
                        {
                            syncProgress = cur;
                            syncCount = count;
                        });
                }
            }
            else
            {
                GUILayout.Label("Syncing " + syncProgress + " / " + (syncCount + 1));
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        private void GetSheetList()
        {
            sheetList = SheetList.Get();
            if (sheetList == null)
            {
                SheetList.Create();
                sheetList = SheetList.Get();
            }
        }

        public class NewSheetWindow : EditorWindow
        {
            public System.Action<SheetList.Sheet> OnAdd;

            private string url;
            private string id;
            private string gid;
            private string filename;
            private string comment;

            private void OnGUI()
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("Enter Google Sheet URL:");

                    EditorGUI.BeginChangeCheck();
                    url = EditorGUILayout.TextField(url);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SheetList.Sheet.TryParseURL(url, out id, out gid);
                    }

                    GUI.enabled = false;

                    GUILayout.Label("ID:");
                    EditorGUILayout.TextField(id);

                    GUILayout.Label("GID:");
                    EditorGUILayout.TextField(gid);

                    GUI.enabled = true;

                    GUILayout.Label("Local filename:");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Browse", GUILayout.Width(100)))
                        {
                            filename = EditorUtility.SaveFilePanelInProject("Save CSV", "data.csv", "csv", "Save you data here.");
                        }

                        filename = EditorGUILayout.TextField(filename);
                    }

                    GUILayout.Label("Comment:");
                    comment = EditorGUILayout.TextField(comment);

                    GUILayout.FlexibleSpace();

                    // If all is cool, add this to the sheet list
                    if (GUILayout.Button("OK"))
                    {
                        OnAdd?.Invoke(SheetList.Sheet.Create(id, gid, filename, comment));
                        Close();
                    }
                }
            }
        }
    }
}