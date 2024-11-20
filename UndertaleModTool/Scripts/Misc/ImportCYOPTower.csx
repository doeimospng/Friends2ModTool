//version 1.0
//By AwfulNasty
//Texture packer by Samuel Roy
//Uses code from https://github.com/mfascia/TexturePacker alongside code from the ImportGraphics script
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using UndertaleModLib.Util;
using UndertaleModLib;
using UndertaleModLib.Models;
using UndertaleModLib.Scripting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using UndertaleModTool;

EnsureDataLoaded();
string towerDirectory = FindTowerPath();
bool getSprites = ScriptQuestion(@"Import sprites from directory " + towerDirectory.Replace(@"/", @"\") + "sprites" + " ?");
if (getSprites == true)
    ImportSprites();
bool getRooms = ScriptQuestion(@"Import levels from directory " + towerDirectory.Replace(@"/", @"\") + "levels" + " ?");
uint lastlayerID = 0;
uint lastInstanceID = 0;
uint lastObjectID = 1123;
IList<UndertaleRoom> roomCheck = Data.Rooms;
foreach (UndertaleRoom checkRoom in roomCheck)
{
    foreach (UndertaleRoom.Layer checkLayer in checkRoom.Layers)
    {
        while (checkLayer.LayerId > lastlayerID)
            lastlayerID++;
    }
    foreach (UndertaleRoom.GameObject checkInstance in checkRoom.GameObjects)
    {
        while (checkInstance.InstanceID > lastInstanceID)
            lastInstanceID++;
    }
}

if (getRooms == true)
    ImportRooms();

#region init
string FindTowerPath()
{
    bool executeScript = ScriptQuestion(@"This script takes a selected tower folder produced by CYOP and imports each room and sprite individually.
Room names will be automatically assigned based on folder names. This does not include the name of the tower.
Custom audio will NOT be imported.
If vanilla tilesets were used, they MUST be included in your sprites folder with the same name they're listed under in CYOP.
You MUST use this only on Pizza Tower v1.0.311, otherwise objects will be messed up!
Would you like to continue?");
    if (!executeScript)
        throw new ScriptException("Script cancelled.");
    string importFolderPath = PromptChooseDirectory();
    if (importFolderPath == null)
        throw new ScriptException("Import folder was not set.");
    return (importFolderPath);
    /*
    string levelsFolderPath = importFolderPath + "\\levels";
    string spritesFolderPath = importFolderPath + "\\sprites";
    string currSpriteName = null;
    string[] levelFiles = Directory.GetFiles(levelsFolderPath, "*.json", SearchOption.AllDirectories);
    string[] spriteFiles = Directory.GetFiles(spritesFolderPath, "*.png", SearchOption.AllDirectories);
    */
}
#endregion

#region sprites
void ImportSprites()
{
    //directly taken from ImportGraphics script
    string packDir = Path.Combine(ExePath, "Packager");
    Directory.CreateDirectory(packDir);

    string sourcePath = towerDirectory + "\\sprites";
    string searchPattern = "*.png";
    string outName = Path.Combine(packDir, "atlas.txt");
    int textureSize = 16384;
    int PaddingValue = 2;
    bool debug = false;
    Packer packer = new Packer();
    packer.Process(sourcePath, searchPattern, textureSize, PaddingValue, debug);
    packer.SaveAtlasses(outName);

    int lastTextPage = Data.EmbeddedTextures.Count - 1;
    int lastTextPageItem = Data.TexturePageItems.Count - 1;

    // Import everything into UTMT
    string prefix = outName.Replace(Path.GetExtension(outName), "");
    int atlasCount = 0;
    foreach (Atlas atlas in packer.Atlasses)
    {
        string atlasName = Path.Combine(packDir, String.Format(prefix + "{0:000}" + ".png", atlasCount));
        Bitmap atlasBitmap = new Bitmap(atlasName);
        UndertaleEmbeddedTexture texture = new UndertaleEmbeddedTexture();
        texture.Name = new UndertaleString("Texture " + ++lastTextPage);
        texture.TextureData.TextureBlob = File.ReadAllBytes(atlasName);
        Data.EmbeddedTextures.Add(texture);
        foreach (Node n in atlas.Nodes)
        {
            if (n.Texture != null)
            {
                // Initalize values of this texture
                var frm = 0;
                int imgs = GetImageCount(n.Texture.Source, n.Texture.Width);
                while (frm < imgs)
                {
                    ushort images = (ushort)imgs;
                    ushort framenumber = (ushort)frm;
                    UndertaleTexturePageItem texturePageItem = new UndertaleTexturePageItem();
                    texturePageItem.Name = new UndertaleString("PageItem " + ++lastTextPageItem);
                    texturePageItem.SourceX = (ushort)(n.Bounds.X + (framenumber * n.Bounds.Width / images));
                    texturePageItem.SourceY = (ushort)n.Bounds.Y;
                    texturePageItem.SourceWidth = (ushort)(n.Bounds.Width / images);
                    texturePageItem.SourceHeight = (ushort)n.Bounds.Height;
                    texturePageItem.TargetX = 0;
                    texturePageItem.TargetY = 0;
                    texturePageItem.TargetWidth = (ushort)(n.Bounds.Width / images);
                    texturePageItem.TargetHeight = (ushort)n.Bounds.Height;
                    texturePageItem.BoundingWidth = (ushort)(n.Bounds.Width / images);
                    texturePageItem.BoundingHeight = (ushort)n.Bounds.Height;
                    texturePageItem.TexturePage = texture;

                    // Add this texture to UMT
                    Data.TexturePageItems.Add(texturePageItem);

                    // String processing
                    string stripped = Path.GetFileNameWithoutExtension(n.Texture.Source);

                    SpriteType spriteType = GetSpriteType(n.Texture.Source);

                    if (spriteType == SpriteType.Tileset)
                    {
                        UndertaleBackground background = Data.Backgrounds.ByName(stripped);
                        uint tileDims = GetTileDimensions(n.Texture.Source);
                        uint tilesetHeight = (uint)n.Bounds.Height;
                        uint tilesetWidth = (uint)n.Bounds.Width;
                        int wAdd = 0;
                        int hAdd = 0;
                        while (tilesetHeight % tileDims != 0)
                        {
                            tilesetHeight++;
                            hAdd++;
                        }
                        while (tilesetWidth % tileDims != 0) //hacky fix for tilesets that aren't wide enough.
                        {
                            tilesetWidth++;
                            wAdd++;
                        }
                        if (wAdd != 0 || hAdd != 0)
                            ScriptMessage($"The tileset {stripped} is not evenly divisible by its tile dimensions!\nThis will cause issues when rendering tiles!\n\nThe tileset needs to be {wAdd} pixels wider and {hAdd} pixels taller!");
                        texturePageItem.BoundingWidth = (ushort)tilesetWidth;
                        texturePageItem.BoundingHeight = (ushort)tilesetHeight;
                        uint tileColumns = (uint)(tilesetWidth / tileDims); //Columns = image width / tile width
                        uint tileRows = (uint)(tilesetHeight / tileDims); //Rows = image height / tile height
                        if (background != null)
                        {
                            background.Texture = texturePageItem;
                            background.Transparent = false;
                            background.Preload = false;
                            background.Texture = texturePageItem;
                            background.GMS2UnknownAlways2 = 2;
                            background.GMS2TileWidth = tileDims;
                            background.GMS2TileHeight = tileDims;
                            background.GMS2OutputBorderX = 0;
                            background.GMS2OutputBorderY = 0;
                            background.GMS2TileColumns = tileColumns;
                            background.GMS2ItemsPerTileCount = 1;
                            background.GMS2TileCount = (tileColumns * tileRows);
                            background.GMS2UnknownAlwaysZero = 0;
                            background.GMS2FrameLength = 66666;
                            //create tile id list
                            background.GMS2TileIds = new List<UndertaleBackground.TileID>();
                            //add in tile ids
                            for (int b = 0; b < background.GMS2TileCount * background.GMS2ItemsPerTileCount; b++)
                            {
                                UndertaleBackground.TileID id = new UndertaleBackground.TileID();
                                id.ID = (UInt32)b;
                                background.GMS2TileIds.Add(id);
                            }

                        }
                        else
                        {
                            // No tileset found, let's make one
                            UndertaleString backgroundUTString = Data.Strings.MakeString(stripped);
                            UndertaleBackground newTileset = new UndertaleBackground();
                            newTileset.Name = backgroundUTString;
                            newTileset.Transparent = false;
                            newTileset.Preload = false;
                            newTileset.Texture = texturePageItem;
                            newTileset.GMS2UnknownAlways2 = 2;
                            newTileset.GMS2TileWidth = tileDims;
                            newTileset.GMS2TileHeight = tileDims;
                            newTileset.GMS2OutputBorderX = 0;
                            newTileset.GMS2OutputBorderY = 0;
                            newTileset.GMS2TileColumns = tileColumns;
                            newTileset.GMS2ItemsPerTileCount = 1;
                            newTileset.GMS2TileCount = (tileColumns * tileRows);
                            newTileset.GMS2UnknownAlwaysZero = 0;
                            newTileset.GMS2FrameLength = 66666;
                            Data.Backgrounds.Add(newTileset);
                            //create tile id list
                            newTileset.GMS2TileIds = new List<UndertaleBackground.TileID>();
                            //add in tile ids
                            for (int b = 0; b < newTileset.GMS2TileCount * newTileset.GMS2ItemsPerTileCount; b++)
                            {
                                UndertaleBackground.TileID id = new UndertaleBackground.TileID();
                                id.ID = (UInt32)b;
                                newTileset.GMS2TileIds.Add(id);
                            }

                        }
                    }
                    else if (spriteType == SpriteType.Sprite)
                    {
                        // Get sprite to add this texture to
                        string spriteName = stripped;
                        int frame = frm;
                        UndertaleSprite sprite = null;
                        sprite = Data.Sprites.ByName(spriteName);

                        // Create TextureEntry object
                        UndertaleSprite.TextureEntry texentry = new UndertaleSprite.TextureEntry();
                        texentry.Texture = texturePageItem;

                        // Set values for new sprites
                        if (sprite == null)
                        {
                            UndertaleString spriteUTString = Data.Strings.MakeString(spriteName);
                            UndertaleSprite newSprite = new UndertaleSprite();
                            newSprite.Name = spriteUTString;
                            newSprite.Width = (uint)(n.Bounds.Width / images);
                            newSprite.Height = (uint)n.Bounds.Height;
                            newSprite.MarginLeft = 0;
                            newSprite.MarginRight = (n.Bounds.Width / images) - 1;
                            newSprite.MarginTop = 0;
                            newSprite.MarginBottom = n.Bounds.Height - 1;
                            newSprite.OriginX = GetOrigin(n.Texture.Source, "x", newSprite);
                            newSprite.OriginY = GetOrigin(n.Texture.Source, "y", newSprite);
                            newSprite.IsSpecialType = true;
                            newSprite.SVersion = 3;
                            newSprite.GMS2PlaybackSpeed = 1;
                            newSprite.GMS2PlaybackSpeedType = AnimSpeedType.FramesPerGameFrame;
                            if (frame > 0)
                            {
                                for (int i = 0; i < frame; i++)
                                    newSprite.Textures.Add(null);
                            }
                            newSprite.Textures.Add(texentry);
                            Data.Sprites.Add(newSprite);
                            frm++;
                            continue;
                        }
                        if (frame > sprite.Textures.Count - 1)
                        {
                            while (frame > sprite.Textures.Count - 1)
                            {
                                sprite.Textures.Add(texentry);
                            }
                            frm++;
                            continue;
                        }
                        sprite.Textures[frame] = texentry;
                    }
                frm++;
                }
            }
        }
        // Increment atlas
        atlasCount++;
    }
}

uint GetTileDimensions(string path)
{
    string iniPath = FindIni(path);
    var tileSize = "32";
    if (iniPath == "")
        return 32;
    IniFile ini = new IniFile(iniPath);
    tileSize = ini.Read("size", "tileset");
    return uint.Parse(tileSize);
}

int GetOrigin(string path, string dir, UndertaleSprite sprite)
{
    string iniPath = FindIni(path);
    var offset = "0";
    if (iniPath == "")
        return 0;
    IniFile ini = new IniFile(iniPath);
    if (dir == "x")
    {
        if (ini.KeyExists("x", "offset"))
            offset = ini.Read("x", "offset");
        if (ini.KeyExists("centered", "offset"))
            offset = (sprite.Width / 2).ToString();
    }
    else
    {
        if (ini.KeyExists("y", "offset"))
            offset = ini.Read("y", "offset");
        if (ini.KeyExists("centered", "offset"))
            offset = (sprite.Height / 2).ToString();
    }
    return int.Parse(offset);
}

SpriteType GetSpriteType(string path)
{
    string iniPath = FindIni(path);
    if (iniPath == "")
        return SpriteType.Sprite;
    IniFile ini = new IniFile(iniPath);
    if (!ini.KeyExists("size", "tileset"))
        return SpriteType.Sprite;
    else
        return SpriteType.Tileset;
}

string FindIni(string path)
{
    string prefix = path.Replace(Path.GetExtension(path), "");
    string iniPath = prefix + ".ini";
    if (File.Exists(iniPath))
    {
        return iniPath;
    }
    else return "";
}

int GetImageCount(string filePath, int width)
{
    string iniPath = FindIni(filePath);
    var imgs = 1;
    if (iniPath == "")
        return 1;
    IniFile ini = new IniFile(iniPath);
    if (!ini.KeyExists("images", "properties") && !ini.KeyExists("image_width", "properties"))
        imgs = 1;
    else if (!ini.KeyExists("images", "properties"))
    {
        int framewidth = int.Parse(ini.Read("image_width", "properties"));
        imgs = width / framewidth;
    }
    else
        imgs = int.Parse(ini.Read("images", "properties"));
    string imgstr = imgs.ToString();
    return imgs;
}
#endregion

#region rooms
public class towerRoom
{
    public string level;
    public string name;
    public string source;
    public string dataName;
    public string jsonString;
    public int levelWidth;
    public int levelHeight;
    public int roomX;
    public int roomY;
    public uint realWidth;
    public uint realHeight;
    List<UndertaleRoom.Layer.LayerBackgroundData> levelBackgrounds;
}

void ImportRooms()
{
    string levelPath = towerDirectory + "\\levels";
    DirectoryInfo di = new DirectoryInfo(levelPath);
    FileInfo[] files = di.GetFiles("*.json", SearchOption.AllDirectories);
    List<towerRoom> newrooms = new List<towerRoom>();
    foreach (FileInfo roomTemp in files)
    {
        towerRoom initRoom = new towerRoom();
        initRoom.source = roomTemp.FullName;
        initRoom.name = Path.GetFileNameWithoutExtension(roomTemp.Name).Replace(" ", "_");
        initRoom.level = roomTemp.Directory.Parent.Name.Replace(" ", "_");
        initRoom.dataName = initRoom.level + "_" + initRoom.name;

        if (Data.Rooms.ByName(initRoom.dataName) == null)
            Data.Rooms.Add(createBlankRoom(initRoom));

        newrooms.Add(initRoom);
    }
    foreach (towerRoom importRoom in newrooms)
    {
        ReadRoom(importRoom, newrooms);
    }
}

UndertaleRoom createBlankRoom(towerRoom initRoom)
{
    UndertaleRoom blankRoom = new UndertaleRoom();
    blankRoom.Name = new UndertaleString(initRoom.dataName);
    blankRoom.Caption = null;
    blankRoom.CreationCodeId = null;
    blankRoom.Flags = (UndertaleRoom.RoomEntryFlags)196615;
    blankRoom.World = false;
    blankRoom.Persistent = false;
    blankRoom.Top = 0;
    blankRoom.Bottom = 0;
    blankRoom.Left = 0;
    blankRoom.Right = 0;
    blankRoom.GravityX = 0;
    blankRoom.GravityY = 10;
    blankRoom.MetersPerPixel = 0.1f;
    blankRoom.Speed = 0;
    blankRoom.Height = 540;
    blankRoom.Width = 960;
    blankRoom.BackgroundColor = 0;
    blankRoom.DrawBackgroundColor = false;
    Data.Strings.Add(blankRoom.Name);

    UndertaleRoom.View newView = blankRoom.Views.First<UndertaleRoom.View>();
    newView.Enabled = true;
    newView.ViewX = 0;
    newView.ViewY = 0;
    newView.ViewWidth = 960;
    newView.ViewHeight = 540;
    newView.PortX = 0;
    newView.PortY = 0;
    newView.PortWidth = 1920;
    newView.PortHeight = 1080;
    newView.BorderX = 1280;
    newView.BorderY = 960;
    newView.SpeedX = -1;
    newView.SpeedY = -1;
    newView.ObjectId = null;

    return blankRoom;
}

void ReadRoom(towerRoom newRoom, List<towerRoom> roomList)
{
    string roomJSON = File.ReadAllText(newRoom.source);
    UndertaleRoom currentRoom = Data.Rooms.ByName(newRoom.dataName);
    JObject root = JObject.Parse(roomJSON);
    int roomX = 0;
    int roomY = 0;
    if (root.TryGetValue("properties", out JToken roomProperties))
    {
        foreach (var roomProperty in roomProperties.Children<JProperty>())
        {
            switch (roomProperty.Name)
            {
                case "roomX":
                    newRoom.roomX = (int)roomProperty.Value;
                    roomX = newRoom.roomX;
                    break;
                case "roomY":
                    newRoom.roomY = (int)roomProperty.Value;
                    roomY = newRoom.roomY;
                    break;
                case "levelWidth":
                    newRoom.levelWidth = (int)roomProperty.Value;
                    break;
                case "levelHeight":
                    newRoom.levelHeight = (int)roomProperty.Value;
                    break;
                default:
                    break;
            }
        }
        if (newRoom.levelWidth - newRoom.roomX > 0)
            newRoom.realWidth = (uint)(newRoom.levelWidth - newRoom.roomX);
        if (newRoom.levelHeight - newRoom.roomY > 0)
            newRoom.realHeight = (uint)(newRoom.levelHeight - newRoom.roomY);
        currentRoom.Width = newRoom.realWidth;
        currentRoom.Height = newRoom.realHeight;
    }
    if (root.TryGetValue("backgrounds", out JToken roomBackgrounds))
    {
        
        foreach (var roomBackground in roomBackgrounds.Children<JProperty>())
        {
            UndertaleRoom.Layer newBGLayer = new UndertaleRoom.Layer();
            newBGLayer.ParentRoom = currentRoom;
            newBGLayer.LayerDepth = 600 + int.Parse(roomBackground.Name.ToString());
            newBGLayer.LayerType = UndertaleRoom.LayerType.Background;
            string layername = "BackgroundLayer_" + roomBackground.Name.ToString();
            newBGLayer.LayerName = (layername == null) ? null : new UndertaleString(layername);
            if ((layername != null) && !Data.Strings.Any(s => s == newBGLayer.LayerName))
                Data.Strings.Add(newBGLayer.LayerName);
            newBGLayer.LayerId = lastlayerID;
            lastlayerID++;
            newBGLayer.HSpeed = 0;
            newBGLayer.VSpeed = 0;
            newBGLayer.XOffset = 0;
            newBGLayer.YOffset = 0;
            newBGLayer.IsVisible = true;
            UndertaleRoom.Layer.LayerBackgroundData newBGLayerData = new UndertaleRoom.Layer.LayerBackgroundData();
            newBGLayerData.ParentLayer = newBGLayer;
            newBGLayerData.Visible = true;
            newBGLayerData.Color = 4294967295;
            newBGLayerData.Sprite = null;
            newBGLayerData.TiledVertically = false;
            newBGLayerData.TiledHorizontally = false;
            newBGLayerData.AnimationSpeed = 1;
            newBGLayerData.AnimationSpeedType = AnimationSpeedType.FPS;
            newBGLayerData.Foreground = false;
            newBGLayerData.Stretch = false;
            newBGLayerData.FirstFrame = 0;

            JObject nestedBGProperties = (JObject)roomBackground.Value;
            foreach (var nestedBGProperty in nestedBGProperties.Properties())
            {
                switch (nestedBGProperty.Name)
                {
                    case "x":
                        newBGLayer.XOffset = (float)nestedBGProperty.Value;
                        break;
                    case "y":
                        newBGLayer.YOffset = (float)nestedBGProperty.Value;
                        break;
                    case "hspeed":
                        newBGLayer.HSpeed = (float)nestedBGProperty.Value;
                        break;
                    case "vspeed":
                        newBGLayer.VSpeed = (float)nestedBGProperty.Value;
                        break;
                    case "tile_x":
                        newBGLayerData.TiledHorizontally = (bool)nestedBGProperty.Value;
                        break;
                    case "tile_y":
                        newBGLayerData.TiledVertically = (bool)nestedBGProperty.Value;
                        break;
                    case "image_speed":
                        newBGLayerData.AnimationSpeed = (float)nestedBGProperty.Value;
                        break;
                    case "scroll_x":
                        break;
                    case "scroll_y":
                        break;
                    case "sprite":
                        UndertaleSprite bgSprite = Data.Sprites.ByName(nestedBGProperty.Value.ToString());
                        if (bgSprite is not null)
                            newBGLayerData.Sprite = bgSprite;
                        break;
                    case "panic_sprite":
                        break;

                }
            }
            newBGLayer.Data = newBGLayerData;
            currentRoom.Layers.Add(newBGLayer);
        }
    }
    if (root.TryGetValue("instances", out JToken roomInstancesToken))
    {
        if (roomInstancesToken.Type == JTokenType.Array)
        {
            JArray roomInstances = (JArray)roomInstancesToken;
            foreach (JObject roomInstance in roomInstances)
            {
                JObject variables = (JObject)roomInstance["variables"];
                bool deleted =  (bool) roomInstance["deleted"];
                int oID = (int)roomInstance["object"];
                if (deleted || (oID > lastObjectID) && oID != 1132)
                    continue;
                if (oID == 1132)
                {
                    string assetName = (string)variables["sprite_index"];
                    UndertaleSprite assetSprite = Data.Sprites.ByName(assetName);
                    int[] assetPos = {0, 0};
                    float[] assetScale = {1, 1};
                    if (variables.ContainsKey("x"))
                        assetPos[0] = (int)variables["x"] - roomX;
                    if (variables.ContainsKey("y"))
                        assetPos[1] = (int)variables["y"] - roomY;
                    if (variables.ContainsKey("image_xscale"))
                        assetScale[0] = (float)variables["image_xscale"];
                    if (variables.ContainsKey("image_yscale"))
                        assetScale[1] = (float)variables["image_yscale"];
                    AddAsset(assetSprite, currentRoom, assetPos, assetScale);
                    continue;
                }
                UndertaleRoom.GameObject newObj = new UndertaleRoom.GameObject();
                int objIndex = oID;
                newObj.ObjectDefinition = Data.GameObjects[objIndex];
                bool flipX = false;
                bool flipY = false;
                if (variables.ContainsKey("x"))
                    newObj.X = (int)variables["x"] - roomX;
                if (variables.ContainsKey("y"))
                    newObj.Y = (int)variables["y"] - roomY;
                if (variables.ContainsKey("image_xscale"))
                    newObj.ScaleX = (float)variables["image_xscale"];
                if (variables.ContainsKey("image_yscale"))
                    newObj.ScaleY = (float)variables["image_yscale"];
                if (variables.ContainsKey("flipX"))
                    flipX = (bool)variables["flipX"];
                if (variables.ContainsKey("flipY"))
                    flipY = (bool)variables["flipY"];
                if (newObj.ObjectDefinition.Name.Content == "obj_hallway")
                {
                    if (newObj.Y < 0)
                    {
                        newObj.ObjectDefinition = Data.GameObjects.ByName("obj_verticalhallway");
                        if (newObj.ScaleY > 0)
                            newObj.ScaleY *= -1;
                        newObj.Y += (int)(newObj.ObjectDefinition.Sprite.Height * Math.Abs(newObj.ScaleY));
                    }
                    else if (newObj.Y > currentRoom.Height)
                    {
                        newObj.ObjectDefinition = Data.GameObjects.ByName("obj_verticalhallway");
                        if (newObj.ScaleY < 0)
                            newObj.ScaleY *= -1;
                        newObj.Y -= (int)(newObj.ObjectDefinition.Sprite.Height * Math.Abs(newObj.ScaleY));
                    }
                }
                newObj.InstanceID = lastInstanceID;
                lastInstanceID++;
               
                string createstring = "";
                foreach (var variable in variables)
                {
                    if (variable.Key != "x" && variable.Key != "y" && variable.Key != "flipX" && variable.Key != "flipY" && variable.Key != "image_xscale" && variable.Key != "image_yscale")
                    {
                        UndertaleSprite spriteCandidate = null;
                        spriteCandidate = Data.Sprites.ByName((string)variable.Value);
                        UndertaleRoom roomCandidate = null;
                        roomCandidate = Data.Rooms.ByName((string)variable.Value);
                        UndertaleObject objectCandidate = null;
                        objectCandidate = Data.GameObjects.ByName((string)variable.Value);
                        if (spriteCandidate is not null || roomCandidate is not null || objectCandidate is not null || (string)variable.Value == "false" || (string)variable.Value == "true")
                        {
                            createstring += $"{variable.Key.ToString()} = {variable.Value.ToString()}\n";
                        }
                        else if (variable.Key == "targetRoom")
                        {
                            foreach (towerRoom targetRoom in roomList)
                            {
                                if (variable.Value.ToString() == targetRoom.name && newRoom.level == targetRoom.level)
                                {
                                    createstring += $"targetRoom = " + targetRoom.dataName + "\n";
                                    break;
                                }
                            }
                        }
                        else if (variable.Key == "levelName")
                        {
                            foreach(towerRoom targetRoom in roomList)
                            {
                                if (variable.Value.ToString() + "_main" == targetRoom.dataName)
                                {
                                    createstring += $"targetRoom = " + targetRoom.dataName + "\n";
                                    createstring += $"level = \"{targetRoom.level}\"\n";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            createstring += $"{variable.Key.ToString()} = \"{variable.Value.ToString()}\"\n";
                        }
                    }
                }
                UndertaleCode instanceCC = null;
                if (createstring != "")
                {
                    instanceCC = new UndertaleCode()
                    {
                        LocalsCount = 1
                    };
                    Data.Code.Add(instanceCC);
                    instanceCC.Name = Data.Strings.MakeString("gml_RoomCC_" + newRoom.dataName + "_" + newObj.InstanceID.ToString() + "_Create");
                    UndertaleCodeLocals instanceCClocals = new UndertaleCodeLocals();
                    UndertaleCodeLocals.LocalVar instanceCClocalvar = new UndertaleCodeLocals.LocalVar();
                    instanceCClocalvar.Name = Data.Strings.MakeString("arguments");
                    instanceCClocalvar.Index = 0;
                    instanceCClocals.Name = instanceCC.Name;
                    instanceCClocals.Locals.Add(instanceCClocalvar);
                    Data.CodeLocals.Add(instanceCClocals);
                    try {
                        instanceCC.AppendGML(createstring, Data);
                        newObj.CreationCode = instanceCC;
                    }
                    catch
                    {
                        ScriptMessage($"Creation code for instance id {newObj.InstanceID} type {newObj.ObjectDefinition} in {newRoom.dataName} could not be imported!\n" + createstring);
                    };
                    
                }
                if (flipX)
                {
                    newObj.ScaleX *= -1;
                    newObj.X += (int)((newObj.ObjectDefinition.Sprite.Width - (2 * newObj.ObjectDefinition.Sprite.OriginX)) * Math.Abs(newObj.ScaleX));
                }
                if (flipY)
                {
                    newObj.ScaleY *= -1;
                    newObj.X += (int)((newObj.ObjectDefinition.Sprite.Height - newObj.ObjectDefinition.Sprite.OriginY) * Math.Abs(newObj.ScaleY));
                }
                newObj.Color = 4294967295;
                newObj.PreCreateCode = null;
                newObj.ImageSpeed = 1;
                newObj.ImageIndex = 0;
                newObj.Rotation = 0;
                int objLayer = (int)roomInstance["layer"];
                string objLayerName = $"Instances_{objLayer + 1}";
                UndertaleRoom.Layer instancelayer = null;
                foreach (UndertaleRoom.Layer lay in currentRoom.Layers)
                {
                    if (lay.LayerType == UndertaleRoom.LayerType.Instances && lay.LayerName.Content == objLayerName)
                    {
                        instancelayer = lay;
                    }
                }
                if (instancelayer is null)
                {
                    UndertaleRoom.Layer newInstanceLayer = new()
                    {
                        LayerName = Data.Strings.MakeString(objLayerName),
                        LayerId = lastlayerID,
                        LayerType = UndertaleRoom.LayerType.Instances,
                        LayerDepth = (int)(5 + objLayer),
                    };
                    lastlayerID++;
                    UndertaleRoom.Layer.LayerInstancesData newInstanceLayerData = new UndertaleRoom.Layer.LayerInstancesData();
                    newInstanceLayer.Data = newInstanceLayerData;
                    currentRoom.Layers.Add(newInstanceLayer);
                    instancelayer = newInstanceLayer;
                }
                UndertaleRoom.Layer.LayerInstancesData instanceLayerData = (UndertaleRoom.Layer.LayerInstancesData)instancelayer.Data;
                instanceLayerData.Instances.Add(newObj);
                currentRoom.GameObjects.Add(newObj);
            }
            Data.GeneralInfo.LastObj = lastInstanceID;
        }
    }
    if (root.TryGetValue("tile_data", out JToken roomTileData))
    {
        bool shownWarning = false;
        foreach (var tiledataToken in roomTileData.Children<JProperty>())
        {
            TowerTile newTile = new TowerTile();
            newTile.tileLayerDepth = int.Parse(tiledataToken.Name);
            newTile.GetTileLayerProperties(newTile);
            JObject layerTileData = tiledataToken.Value as JObject;
            foreach (var tileObj in layerTileData.Properties())
            {
                JObject tileProperties = tileObj.Value as JObject;
                foreach (var tileProp in tileProperties.Properties())
                {
                    switch (tileProp.Name)
                    {
                        case "coord":
                            JArray tilesetCoords = (JArray)tileProp.Value;
                            newTile.row = (int)tilesetCoords[1];
                            newTile.column = (int)tilesetCoords[0];
                            break;
                        case "tileset":
                            newTile.tileset = Data.Backgrounds.ByName((string)tileProp.Value);
                            if (newTile.tileset == null && shownWarning == false)
                            {
                                ScriptMessage($"Could not find tileset {tileProp.Value}! Did you include it in your Sprites folder?");
                                shownWarning = true;
                                continue;
                            }
                            break;
                    }
                }
                if (newTile.tileset != null)
                {
                    string[] tileCoords = tileObj.Name.Split('_');
                    newTile.X = (int.Parse(tileCoords[0]) - roomX) / (int)newTile.tileset.GMS2TileWidth;
                    newTile.Y = (int.Parse(tileCoords[1]) - roomY) / (int)newTile.tileset.GMS2TileHeight;
                    SetTileLayer(newTile, currentRoom);
                }
            }
        }
    }
}

void AddAsset(UndertaleSprite sprite, UndertaleRoom room, int[] position, float[] scale)
{
    int assetX = position[0];
    int assetY = position[1];
    float assetXScale = scale[0];
    float assetYScale = scale[1];
    UndertaleRoom.Layer assetLayer = null;
    foreach (UndertaleRoom.Layer searchLay in room.Layers)
    {
        if (searchLay.LayerType == UndertaleRoom.LayerType.Assets)
            assetLayer = searchLay;
    }
    if (assetLayer is null)
    {
        UndertaleRoom.Layer newLayer = new UndertaleRoom.Layer();
        string layerName = "Assets_1";
        newLayer.LayerId = lastlayerID;
        lastlayerID++;
        newLayer.LayerType = UndertaleRoom.LayerType.Assets;
        newLayer.LayerDepth = 100;
        newLayer.XOffset = 0;
        newLayer.YOffset = 0;
        newLayer.HSpeed = 0;
        newLayer.VSpeed = 0;
        newLayer.IsVisible = true;
        newLayer.LayerName = (layerName == null) ? null : new UndertaleString(layerName);
        if ((layerName != null) && !Data.Strings.Any(s => s == newLayer.LayerName))
            Data.Strings.Add(newLayer.LayerName);
        UndertaleRoom.Layer.LayerAssetsData newLayerData = new UndertaleRoom.Layer.LayerAssetsData();
        newLayerData.LegacyTiles = new UndertalePointerList<UndertaleRoom.Tile>();
        newLayerData.Sprites = new UndertalePointerList<UndertaleRoom.SpriteInstance>();
        newLayerData.Sequences = new UndertalePointerList<UndertaleRoom.SequenceInstance>();
        newLayerData.NineSlices = new UndertalePointerList<UndertaleRoom.SpriteInstance>();
        newLayer.Data = newLayerData;
        assetLayer = newLayer;
        room.Layers.Add(assetLayer);
    }
    UndertaleRoom.SpriteInstance newSpr = new UndertaleRoom.SpriteInstance();
    string name = "Asset_" + sprite.Name.Content;
    UndertaleRoom.Layer.LayerAssetsData AssetLayerData = assetLayer.AssetsData;
    newSpr.Name = new UndertaleString(name);
    if ((name != null) && !Data.Strings.Any(s => s == newSpr.Name))
        Data.Strings.Add(newSpr.Name);
    newSpr.X = assetX;
    newSpr.Y = assetY;
    newSpr.ScaleX = assetXScale;
    newSpr.ScaleY = assetYScale;
    newSpr.Color = 4294967295;
    newSpr.AnimationSpeed = 15;
    newSpr.AnimationSpeedType = AnimationSpeedType.FPS;
    newSpr.FrameIndex = 0;
    newSpr.Rotation = 0;
    newSpr.Sprite = sprite;
    AssetLayerData.Sprites.Add(newSpr);
}
void SetTileLayer(TowerTile tile, UndertaleRoom room)
{
    UndertaleRoom.Layer layer = null;
    foreach (UndertaleRoom.Layer lay in room.Layers)
    {
        if (lay.LayerType == UndertaleRoom.LayerType.Tiles && lay.LayerName.Content == tile.tileLayerName)
        {
            UndertaleRoom.Layer.LayerTilesData laydata = lay.Data as UndertaleRoom.Layer.LayerTilesData;
            if (laydata.Background == tile.tileset)
                layer = lay;
        }
    }
    if (layer is null)
    {
        UndertaleRoom.Layer newTilesLayer = new()
        {
            LayerName = Data.Strings.MakeString(tile.tileLayerName),
            LayerId = lastlayerID,
            LayerType = UndertaleRoom.LayerType.Tiles,
            LayerDepth = tile.realDepth,
            IsVisible = true,
        };
        lastlayerID++;
        UndertaleRoom.Layer.LayerTilesData newTilesLayerData = new UndertaleRoom.Layer.LayerTilesData();
        newTilesLayerData.Background = tile.tileset;
        newTilesLayer.Data = newTilesLayerData;
        uint tileLayerWidth = room.Width;
        uint tileLayerHeight = room.Height;
        while (tileLayerWidth % tile.tileset.GMS2TileWidth != 0)
        {
            tileLayerWidth++;
        }
        while (tileLayerHeight % tile.tileset.GMS2TileHeight != 0)
        {
            tileLayerHeight++;
        }
        newTilesLayer.TilesData.TilesX = tileLayerWidth / tile.tileset.GMS2TileWidth;
        newTilesLayer.TilesData.TilesY = tileLayerHeight / tile.tileset.GMS2TileHeight;
        uint[][] newTileIDs = new uint[newTilesLayer.TilesData.TilesY][];
        for (int dataRow = 0; dataRow < newTilesLayer.TilesData.TilesY; dataRow++)
        {
            newTileIDs[dataRow] = new uint[newTilesLayer.TilesData.TilesX];
        }
        for (int iRow = 0; iRow < newTilesLayer.TilesData.TilesY; iRow++)
        {
            for (int iCol = 0; iCol <  newTilesLayer.TilesData.TilesX; iCol++)
            {
                (newTileIDs[iRow])[iCol] = 0;
            }
        }
        newTilesLayer.TilesData.TileData = newTileIDs;
        room.Layers.Add(newTilesLayer);
        layer = newTilesLayer;

    }
    int tileIDRow = tile.row;
    uint tid = 0;
    while (tileIDRow > 0)
    {
        tid += tile.tileset.GMS2TileColumns;
        tileIDRow--;
    }
    tid += (uint)tile.column;
    tile.ID = tid;
    if (tile.ID > tile.tileset.GMS2TileCount - 1 || tile.ID < 0)
    {
        tile.ID = 0;
    }
    uint[][] tileIDs = layer.TilesData.TileData;
    if (tile.X > layer.TilesData.TilesX || tile.Y > layer.TilesData.TilesY || tile.X < 0 || tile.Y < 0)
    {
    }
    else
    {
        try
        {
            (tileIDs[tile.Y])[tile.X] = tile.ID;
        }
        catch { }
    }
}
public class TowerTile
{
    public int tileLayerDepth;
    public int realDepth;
    public string tileLayerName;
    public int X;
    public int Y;
    public uint ID;
    public UndertaleBackground tileset;
    public int row;
    public int column;
    public void GetTileLayerProperties(TowerTile tile)
    {
        string name = $"Tiles_{tile.tileLayerDepth}";
        int depth = 100;
        switch (tile.tileLayerDepth)
        {
            case 10:
                name = "Tiles_BG1";
                depth = 201;
                break;
            case 9:
                name = "Tiles_BG";
                depth = 200;
                break;
            case 8:
                name = "Tiles_BG2";
                depth = 199;
                break;
            case 7:
                name = "Tiles_BG3";
                depth = 198;
                break;
            case 6:
                name = "Tiles_1";
                depth = 100;
                break;
            case 5:
                name = "Tiles_2";
                depth = 98;
                break;
            case 4:
                name = "Tiles_3";
                depth = 97;
                break;
            case 3:
                name = "Tiles_4";
                depth = 96;
                break;
            case 2:
                name = "Tiles_5";
                depth = 96;
                break;
            case 1:
                name = "Tiles_6";
                depth = 95;
                break;
            case 0:
                name = "Tiles_7";
                depth = 94;
                break;
            case -1:
                name = "Tiles_FG1";
                depth = 93;
                break;
            case -2:
                name = "Tiles_FG2";
                depth = 92;
                break;
            case -3:
                name = "Tiles_FG3";
                depth = 91;
                break;
            case -4:
                name = "Tiles_FG4";
                depth = 90;
                break;
            case -5:
                name = "Tiles_Secret1";
                depth = 89;
                break;
            case -6:
                name = "Tiles_Secret2";
                depth = 88;
                break;
            case -7:
                name = "Tiles_Secret3";
                depth = 87;
                break;
            case -8:
                name = "Tiles_Secret3";
                depth = 86;
                break;
        }
        tile.tileLayerName = name;
        tile.realDepth = depth;
    }
}
#endregion

#region texture packer
public class TextureInfo
{
    public string Source;
    public int Width;
    public int Height;
}

public enum SpriteType
{
    Sprite,
    Tileset
}


public enum SplitType
{
    Horizontal,
    Vertical,
}

public enum BestFitHeuristic
{
    Area,
    MaxOneAxis,
}

public class Node
{
    public Rectangle Bounds;
    public TextureInfo Texture;
    public SplitType SplitType;
}

public class Atlas
{
    public int Width;
    public int Height;
    public List<Node> Nodes;
}

public class Packer
{
    public List<TextureInfo> SourceTextures;
    public StringWriter Log;
    public StringWriter Error;
    public int Padding;
    public int AtlasSize;
    public bool DebugMode;
    public BestFitHeuristic FitHeuristic;
    public List<Atlas> Atlasses;

    public Packer()
    {
        SourceTextures = new List<TextureInfo>();
        Log = new StringWriter();
        Error = new StringWriter();
    }

    public void Process(string _SourceDir, string _Pattern, int _AtlasSize, int _Padding, bool _DebugMode)
    {
        Padding = _Padding;
        AtlasSize = _AtlasSize;
        DebugMode = _DebugMode;
        //1: scan for all the textures we need to pack
        ScanForTextures(_SourceDir, _Pattern);
        List<TextureInfo> textures = new List<TextureInfo>();
        textures = SourceTextures.ToList();
        //2: generate as many atlasses as needed (with the latest one as small as possible)
        Atlasses = new List<Atlas>();
        while (textures.Count > 0)
        {
            Atlas atlas = new Atlas();
            atlas.Width = _AtlasSize;
            atlas.Height = _AtlasSize;
            List<TextureInfo> leftovers = LayoutAtlas(textures, atlas);
            if (leftovers.Count == 0)
            {
                // we reached the last atlas. Check if this last atlas could have been twice smaller
                while (leftovers.Count == 0)
                {
                    atlas.Width /= 2;
                    atlas.Height /= 2;
                    leftovers = LayoutAtlas(textures, atlas);
                }
                // we need to go 1 step larger as we found the first size that is to small
                atlas.Width *= 2;
                atlas.Height *= 2;
                leftovers = LayoutAtlas(textures, atlas);
            }
            Atlasses.Add(atlas);
            textures = leftovers;
        }
    }

    public void SaveAtlasses(string _Destination)
    {
        int atlasCount = 0;
        string prefix = _Destination.Replace(Path.GetExtension(_Destination), "");
        string descFile = _Destination;
        StreamWriter tw = new StreamWriter(_Destination);
        tw.WriteLine("source_tex, atlas_tex, x, y, width, height");
        foreach (Atlas atlas in Atlasses)
        {
            string atlasName = String.Format(prefix + "{0:000}" + ".png", atlasCount);
            //1: Save images
            Image img = CreateAtlasImage(atlas);
            img.Save(atlasName, System.Drawing.Imaging.ImageFormat.Png);
            //2: save description in file
            foreach (Node n in atlas.Nodes)
            {
                if (n.Texture != null)
                {
                    tw.Write(n.Texture.Source + ", ");
                    tw.Write(atlasName + ", ");
                    tw.Write((n.Bounds.X).ToString() + ", ");
                    tw.Write((n.Bounds.Y).ToString() + ", ");
                    tw.Write((n.Bounds.Width).ToString() + ", ");
                    tw.WriteLine((n.Bounds.Height).ToString());
                }
            }
            ++atlasCount;
        }
        tw.Close();
        tw = new StreamWriter(prefix + ".log");
        tw.WriteLine("--- LOG -------------------------------------------");
        tw.WriteLine(Log.ToString());
        tw.WriteLine("--- ERROR -----------------------------------------");
        tw.WriteLine(Error.ToString());
        tw.Close();
    }

    private void ScanForTextures(string _Path, string _Wildcard)
    {
        DirectoryInfo di = new DirectoryInfo(_Path);
        FileInfo[] files = di.GetFiles(_Wildcard, SearchOption.AllDirectories);
        foreach (FileInfo fi in files)
        {
            Image img = Image.FromFile(fi.FullName);
            if (img != null)
            {
                if (img.Width <= AtlasSize && img.Height <= AtlasSize)
                {
                    TextureInfo ti = new TextureInfo();
                    ti.Source = fi.FullName;
                    ti.Width = img.Width;
                    ti.Height = img.Height;
                    SourceTextures.Add(ti);

                    Log.WriteLine("Added " + fi.FullName);
                }
                else
                {
                    Error.WriteLine(fi.FullName + " is too large to fix in the atlas. Skipping!");
                }
            }
        }
    }

    private void HorizontalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List)
    {
        Node n1 = new Node();
        n1.Bounds.X = _ToSplit.Bounds.X + _Width + Padding;
        n1.Bounds.Y = _ToSplit.Bounds.Y;
        n1.Bounds.Width = _ToSplit.Bounds.Width - _Width - Padding;
        n1.Bounds.Height = _Height;
        n1.SplitType = SplitType.Vertical;
        Node n2 = new Node();
        n2.Bounds.X = _ToSplit.Bounds.X;
        n2.Bounds.Y = _ToSplit.Bounds.Y + _Height + Padding;
        n2.Bounds.Width = _ToSplit.Bounds.Width;
        n2.Bounds.Height = _ToSplit.Bounds.Height - _Height - Padding;
        n2.SplitType = SplitType.Horizontal;
        if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
            _List.Add(n1);
        if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
            _List.Add(n2);
    }

    private void VerticalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List)
    {
        Node n1 = new Node();
        n1.Bounds.X = _ToSplit.Bounds.X + _Width + Padding;
        n1.Bounds.Y = _ToSplit.Bounds.Y;
        n1.Bounds.Width = _ToSplit.Bounds.Width - _Width - Padding;
        n1.Bounds.Height = _ToSplit.Bounds.Height;
        n1.SplitType = SplitType.Vertical;
        Node n2 = new Node();
        n2.Bounds.X = _ToSplit.Bounds.X;
        n2.Bounds.Y = _ToSplit.Bounds.Y + _Height + Padding;
        n2.Bounds.Width = _Width;
        n2.Bounds.Height = _ToSplit.Bounds.Height - _Height - Padding;
        n2.SplitType = SplitType.Horizontal;
        if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
            _List.Add(n1);
        if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
            _List.Add(n2);
    }

    private TextureInfo FindBestFitForNode(Node _Node, List<TextureInfo> _Textures)
    {
        TextureInfo bestFit = null;
        float nodeArea = _Node.Bounds.Width * _Node.Bounds.Height;
        float maxCriteria = 0.0f;
        foreach (TextureInfo ti in _Textures)
        {
            switch (FitHeuristic)
            {
                // Max of Width and Height ratios
                case BestFitHeuristic.MaxOneAxis:
                    if (ti.Width <= _Node.Bounds.Width && ti.Height <= _Node.Bounds.Height)
                    {
                        float wRatio = (float)ti.Width / (float)_Node.Bounds.Width;
                        float hRatio = (float)ti.Height / (float)_Node.Bounds.Height;
                        float ratio = wRatio > hRatio ? wRatio : hRatio;
                        if (ratio > maxCriteria)
                        {
                            maxCriteria = ratio;
                            bestFit = ti;
                        }
                    }
                    break;
                // Maximize Area coverage
                case BestFitHeuristic.Area:
                    if (ti.Width <= _Node.Bounds.Width && ti.Height <= _Node.Bounds.Height)
                    {
                        float textureArea = ti.Width * ti.Height;
                        float coverage = textureArea / nodeArea;
                        if (coverage > maxCriteria)
                        {
                            maxCriteria = coverage;
                            bestFit = ti;
                        }
                    }
                    break;
            }
        }
        return bestFit;
    }

    private List<TextureInfo> LayoutAtlas(List<TextureInfo> _Textures, Atlas _Atlas)
    {
        List<Node> freeList = new List<Node>();
        List<TextureInfo> textures = new List<TextureInfo>();
        _Atlas.Nodes = new List<Node>();
        textures = _Textures.ToList();
        Node root = new Node();
        root.Bounds.Size = new Size(_Atlas.Width, _Atlas.Height);
        root.SplitType = SplitType.Horizontal;
        freeList.Add(root);
        while (freeList.Count > 0 && textures.Count > 0)
        {
            Node node = freeList[0];
            freeList.RemoveAt(0);
            TextureInfo bestFit = FindBestFitForNode(node, textures);
            if (bestFit != null)
            {
                if (node.SplitType == SplitType.Horizontal)
                {
                    HorizontalSplit(node, bestFit.Width, bestFit.Height, freeList);
                }
                else
                {
                    VerticalSplit(node, bestFit.Width, bestFit.Height, freeList);
                }
                node.Texture = bestFit;
                node.Bounds.Width = bestFit.Width;
                node.Bounds.Height = bestFit.Height;
                textures.Remove(bestFit);
            }
            _Atlas.Nodes.Add(node);
        }
        return textures;
    }

    private Image CreateAtlasImage(Atlas _Atlas)
    {
        Image img = new Bitmap(_Atlas.Width, _Atlas.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(img);
        foreach (Node n in _Atlas.Nodes)
        {
            if (n.Texture != null)
            {
                Image sourceImg = Image.FromFile(n.Texture.Source);
                g.DrawImage(sourceImg, n.Bounds);
            }
        }
        // DPI FIX START
        Bitmap ResolutionFix = new Bitmap(img);
        ResolutionFix.SetResolution(150.0F, 150.0F); //Upped resolution from 96 to 150 due to color blending in larger sprites.
        Image img2 = ResolutionFix;
        return img2;
        // DPI FIX END
    }
}
#endregion

#region ini parser
class IniFile   // by Danny Beckett
{
    string Path;
    string EXE = Assembly.GetExecutingAssembly().GetName().Name;

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

    public IniFile(string IniPath = null)
    {
        Path = new FileInfo(IniPath ?? EXE + ".ini").FullName;
    }

    public string Read(string Key, string Section = null)
    {
        var RetVal = new StringBuilder(255);
        GetPrivateProfileString(Section ?? EXE, Key, "", RetVal, 255, Path);
        return RetVal.ToString();
    }

    public void Write(string Key, string Value, string Section = null)
    {
        WritePrivateProfileString(Section ?? EXE, Key, Value, Path);
    }

    public void DeleteKey(string Key, string Section = null)
    {
        Write(Key, null, Section ?? EXE);
    }

    public void DeleteSection(string Section = null)
    {
        Write(null, null, Section ?? EXE);
    }

    public bool KeyExists(string Key, string Section = null)
    {
        return Read(Key, Section).Length > 0;
    }
}
#endregion