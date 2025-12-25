using System.Collections.Generic;
using System.Windows.Forms;

namespace AssetStudio.GUI
{
    public class DuplicateAssetInfo
    {
        public string Name { get; set; }
        public long PathID { get; set; }
        public string Container { get; set; }
        
        public DuplicateAssetInfo(string name, long pathID, string container)
        {
            Name = name;
            PathID = pathID;
            Container = container;
        }
    }
    
    public class AssetItem : ListViewItem
    {
        public Object Asset;
        public SerializedFile SourceFile;
        public string Container = string.Empty;
        public string TypeString;
        public long m_PathID;
        public long FullSize;
        public ClassIDType Type;
        public string InfoText;
        public string UniqueID;
        public int Gesamtzahl;
        public GameObjectTreeNode TreeNode;
        public string AllContainer = string.Empty;
        public List<long> SubItemValues = new List<long>();
        public List<DuplicateAssetInfo> DuplicateAssets = new List<DuplicateAssetInfo>();

        public AssetItem(Object asset)
        {
            Asset = asset;
            Text = asset.Name;
            SourceFile = asset.assetsFile;
            Type = asset.type;
            TypeString = Type.ToString();
            m_PathID = asset.m_PathID;
            FullSize = asset.byteSize;
            Gesamtzahl = 1;
        }

        public void SetSubItems()
        {
            SubItems.AddRange(new[]
            {
                Container, //Container
                TypeString, //Type
                m_PathID.ToString(), //PathID
                FullSize.ToString(), //Size
            });
        }

        public void SetSubItems2()
        {
            SubItems.AddRange(new[]
            {
                m_PathID.ToString(), //PathID
                TypeString, //Type
                Gesamtzahl.ToString(),
                ((float)FullSize/(1024 * 1024)).ToString("F2")+" MB", //Size
                ((Gesamtzahl-1)*(float)FullSize/(1024 * 1024)).ToString("F2")+" MB"
            });
        }
    }
}
