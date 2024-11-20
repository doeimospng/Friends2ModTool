//By AwfulNasty
//Version 1.3.0
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UndertaleModLib.Util;
using UndertaleModLib;
using UndertaleModLib.Models;
using UndertaleModLib.Scripting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using UndertaleModLib.Decompiler;
using System.Threading;
using static UndertaleModLib.Models.UndertaleRoom;
using Microsoft.CodeAnalysis;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Data;
using static UndertaleModLib.Models.UndertaleGameObject;

EnsureDataLoaded();
#region inits
//excuse the mess below
bool export = true; string projectFolder = null; string dataPath = null; bool getSprites = false; bool getObjects = false; bool getCode = false; bool getRooms = false; UndertaleData vData = null; bool comparePixels = true;
IList<UndertaleSprite> ExportSprites = null; IList<UndertaleBackground> ExportTilesets = null; IList<UndertaleGameObject> ExportObjects = null; IList<UndertaleCode> ExportCode = null; IList<UndertaleScript> ExportScript = null; IList<UndertaleRoom> ExportRooms = null; IList<UndertaleCode> ExportFunctions = null; IgnoreList ignoreList = new IgnoreList();
#endregion
IntroForm form = new IntroForm();
export = form.export; projectFolder = form.projectFolder;
if (projectFolder is null) { return; }
if (form.ignoreList is not null) {ignoreList = JsonConvert.DeserializeObject<IgnoreList>(form.ignoreList); }
SetProgressBar(null, "Loading", 0, 1);
StartProgressBarUpdater();
if (export)
{
    dataPath = form.dataLocation;
    getSprites = form.getSprites; getObjects = form.getObjects; getCode = form.getCode; getRooms = form.getRooms; comparePixels = form.comparePixels;

    vData = null;
    try
    {
        using (var stream = new FileStream(dataPath, FileMode.Open, FileAccess.Read))
        {
            vData = UndertaleIO.Read(stream, null);
        }

    }
    catch
    {
        throw new ScriptException("The comparison data file could not be loaded.");
    }
    if (vData == null)
        throw new ScriptException("The comparison data file could not be loaded.");

    ExportSprites = new List<UndertaleSprite>();
    ExportTilesets = new List<UndertaleBackground>();
    ExportObjects = new List<UndertaleGameObject>();
    ExportCode = new List<UndertaleCode>();
    ExportFunctions = new List<UndertaleCode>();
    ExportScript = new List<UndertaleScript>();
    ExportRooms = new List<UndertaleRoom>();
    await DoComparison();
    await StopProgressBarUpdater();
}
else if (export == false)
{
    getSprites = form.getSprites; getObjects = form.getObjects; getCode = form.getCode; getRooms = form.getRooms; comparePixels = form.comparePixels;
    await DoImport();
    await StopProgressBarUpdater();
}

async Task DoImport()
{
    List<ObjectPropertyContainer>importedObjects = new List<ObjectPropertyContainer>();
    List<RoomPropertyContainer>importedRooms = new List<RoomPropertyContainer>();
    if (getSprites == true)
    {
        string packDir = Path.Combine(ExePath, "Packager");
        string spriteDir = Path.Combine(projectFolder, "sprites");
        string tilesetDir = Path.Combine(projectFolder, "tilesets");
        if (Directory.Exists(spriteDir))
        {
            string[] spriteDirs = Directory.GetDirectories(spriteDir);
            SetProgressBar(null, "Importing Sprites", 0, spriteDirs.Length);
            ImportSprites(projectFolder, true);
            await Task.Run(() => Parallel.ForEach(spriteDirs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, dir =>
            {
                try
                {
                    string jsonFile = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories).FirstOrDefault();
                    string json = File.ReadAllText(jsonFile);
                    SpritePropertyContainer spriteProperties = JsonConvert.DeserializeObject<SpritePropertyContainer>(json);
                    if (ignoreList.Sprites.Contains(spriteProperties.Name)) { return; }
                    UndertaleSprite spr = Data.Sprites.ByName(spriteProperties.Name);
                    spr.BBoxMode = spriteProperties.BBMode;
                    spr.GMS2PlaybackSpeed = spriteProperties.PlaybackSpeed;
                    spr.GMS2PlaybackSpeedType = spriteProperties.PlaybackType;
                    spr.Height = spriteProperties.Height;
                    spr.Width = spriteProperties.Width;
                    spr.OriginX = spriteProperties.Origin[0];
                    spr.OriginY = spriteProperties.Origin[1];
                    spr.MarginLeft = spriteProperties.Margin[0];
                    spr.MarginRight = spriteProperties.Margin[1];
                    spr.MarginBottom = spriteProperties.Margin[2];
                    spr.MarginTop = spriteProperties.Margin[3];
                    spr.SVersion = spriteProperties.SpecialVersion;
                    spr.Transparent = spriteProperties.Transparent;
                    spr.SepMasks = spriteProperties.SMaskType;
                    spr.Smooth = spriteProperties.isSmooth;
                    spr.Preload = spriteProperties.isPreload;
                    spr.IsSpecialType = spriteProperties.isSpecial;
                    if (spr.CollisionMasks is not null && spr.CollisionMasks.Count > 0)
                    {
                        if (spr.CollisionMasks[0].Data.Length != spr.Width * spr.Height)
                        {
                            spr.CollisionMasks.Clear();
                            ScriptMessage($"Removed generated collision mask from sprite {spr.Name.Content}.\nSprite and Collision Mask dimensions not equal!");
                        }
                    }
                }
                catch
                {
                    ScriptMessage($"Something went wrong while importing JSON data for\n{dir}\nProperties may need to be imported manually.");
                }
                IncrementProgressParallel();
            }));
        }
        if (Directory.Exists(tilesetDir))
        {
            string[] tilesetDirs = Directory.GetDirectories(tilesetDir);
            SetProgressBar(null, "Importing Tilesets", 0, tilesetDirs.Length);
            await Task.Run(() => Parallel.ForEach(tilesetDirs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, dir =>
            {
                try
                {
                    string jsonFile = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories).FirstOrDefault();
                    string json = File.ReadAllText(jsonFile);
                    TilesetPropertyContainer tilesetProperties = JsonConvert.DeserializeObject<TilesetPropertyContainer>(json);
                    if (ignoreList.Tilesets.Contains(tilesetProperties.Name)) { return; }
                    UndertaleBackground tileset = Data.Backgrounds.ByName(tilesetProperties.Name);
                    tileset.GMS2TileHeight = tilesetProperties.TileHeight;
                    tileset.GMS2TileWidth = tilesetProperties.TileWidth;
                    tileset.GMS2TileColumns = tilesetProperties.TileColumns;
                    tileset.GMS2TileCount = tilesetProperties.TileCount;
                    tileset.GMS2ItemsPerTileCount = tilesetProperties.ItemsPerTile;
                    tileset.GMS2OutputBorderX = tilesetProperties.BorderX;
                    tileset.GMS2OutputBorderY = tilesetProperties.BorderY;
                    //add in tile ids
                    for (int b = 0; b < tileset.GMS2TileCount * tileset.GMS2ItemsPerTileCount; b++)
                    {
                        UndertaleBackground.TileID id = new UndertaleBackground.TileID();
                        id.ID = (UInt32)b;
                        tileset.GMS2TileIds.Add(id);
                    }
                }
                catch
                {
                    ScriptMessage($"Something went wrong while importing JSON data for\n{dir}\nProperties may need to be imported manually.");
                }
                IncrementProgressParallel();
            }));
        }

    }
    if (getObjects == true) //first time around, just need to init the new objects
    {
        string objdir = Path.Combine(projectFolder, "objects");
        if (Directory.Exists(objdir))
        {
            string[] objJsons = Directory.GetFiles(objdir, "*.json", SearchOption.AllDirectories);
            foreach(string jsonPath in objJsons) //this one didn't like being parallel
            {
                string json = File.ReadAllText(jsonPath);
                ObjectPropertyContainer objProperties = JsonConvert.DeserializeObject<ObjectPropertyContainer>(json);
                importedObjects.Add(objProperties);
                UndertaleGameObject objCheck;
                objCheck = Data.GameObjects.ByName(objProperties.Name);
                if (objCheck == null)
                {
                    UndertaleString objName = Data.Strings.MakeString(objProperties.Name);
                    UndertaleGameObject newObj = new UndertaleGameObject()
                    {
                        Name = objName
                    };
                    Data.GameObjects.Add(newObj);
                }
            };
        }
    }
    if (getRooms == true) //first time around, just need to init the new rooms
    {
        string roomsdir = Path.Combine(projectFolder, "rooms");
        if (Directory.Exists(roomsdir))
        {
            string[] roomJsons = Directory.GetFiles(roomsdir, "*.json", SearchOption.AllDirectories);
            foreach(string jsonPath in roomJsons)
            {
                string json = File.ReadAllText(jsonPath);
                RoomPropertyContainer roomProperties = JsonConvert.DeserializeObject<RoomPropertyContainer>(json);
                if (ignoreList.Rooms.Contains(roomProperties.Name)) { return; }
                importedRooms.Add(roomProperties);
                UndertaleRoom roomCheck;
                roomCheck = Data.Rooms.ByName(roomProperties.Name);
                if (roomCheck == null)
                {
                    UndertaleString roomName = Data.Strings.MakeString(roomProperties.Name);
                    UndertaleRoom newRoom = new UndertaleRoom()
                    {
                        Name = roomName
                    };
                    Data.Rooms.Add(newRoom);
                }
            };
        }
    }
    if (getCode == true)
    {
        SetProgressBar(null, "Importing Scripts and Functions", 0, 1);
        string functionsdir = Path.Combine(projectFolder, "functions");
        List<JObject> Functions = new List<JObject>();
        if (Directory.Exists(functionsdir))
        {
            string functionsJson = Directory.GetFiles(functionsdir, "*.json", SearchOption.AllDirectories).FirstOrDefault();
            string json = File.ReadAllText(functionsJson);
            JArray FunctionContainer = JArray.Parse(json);
            foreach (JObject func in FunctionContainer)
            {
                UndertaleCode codeCheck;
                string sName = (string)func["Name"];
                string cName = (string)func["Code"];
                codeCheck = Data.Code.ByName(cName);
                if (codeCheck is null)
                {
                    ImportGMLString(cName, "", false, false);
                    codeCheck = Data.Code.ByName(cName);
                }
                List<string> codeFunctions = new List<string>();
                foreach(UndertaleCode childfunc in codeCheck.ChildEntries)
                {
                    codeFunctions.Add(childfunc.Name.Content);
                }
                codeFunctions.Add(sName);
                List<string> codeNames = codeFunctions.Select(x => x.Substring(11)).ToList(); //remove gml_Script_
                string justFunctions = "";
                foreach (string fName in  codeNames)
                {
                    justFunctions += $"function {fName}()\n{{\n}}";
                }
                ImportGMLString(cName, justFunctions, false, false);
            }
        }
        string scriptsdir = Path.Combine(projectFolder, "scripts");
        List<JObject> Scripts = new List<JObject>();
        if (Directory.Exists(scriptsdir))
        {
            string scriptsJson = Directory.GetFiles(scriptsdir, "*.json", SearchOption.AllDirectories).FirstOrDefault();
            string json = File.ReadAllText(scriptsJson);
            JArray ScriptContainer = JArray.Parse(json);
            foreach(JObject script in ScriptContainer)
            {
                UndertaleScript scriptCheck;
                string sName = (string)script["Name"];
                if (ignoreList.Scripts.Contains(sName)) { return; }
                scriptCheck = Data.Scripts.ByName(sName);
                if (scriptCheck == null)
                {
                    UndertaleString ScriptName = Data.Strings.MakeString(sName);
                    UndertaleScript newScript = new UndertaleScript()
                    {
                        Name = ScriptName
                    };
                    Data.Scripts.Add(newScript);
                }
                Scripts.Add(script);
            };
        }
        string codedir = Path.Combine(projectFolder, "code");

        if (Directory.Exists(codedir))
        {
            string[] gmlFiles = Directory.GetFiles(codedir, "*.gml", SearchOption.AllDirectories);
            SetProgressBar(null, "Importing Code", 0, gmlFiles.Length);
            foreach (string gmlFile in gmlFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(gmlFile);
                if (ignoreList.Code.Contains(fileName)) { return; }
                ImportGMLFile(gmlFile, false, false, false);
                IncrementProgressParallel();
            };
        }
        foreach(JObject script in Scripts)
        {
            JObject Script = (JObject)script;
            string scriptName = (string)Script["Name"];
            string codeName = (string)Script["Code"];
            if (ignoreList.Scripts.Contains(scriptName)) { return; }
            if (ignoreList.Code.Contains(codeName)) { return; }
            UndertaleScript ScriptMatch;
            UndertaleCode CodeMatch;
            ScriptMatch = Data.Scripts.ByName(scriptName);
            CodeMatch = Data.Code.ByName(codeName);
            if (ScriptMatch is not null && CodeMatch is not null)
            {
                ScriptMatch.Code = CodeMatch;
            }
        };
    }
    if (importedObjects.Count > 0) { SetProgressBar(null, "Importing Objects", 0, importedObjects.Count); }
    foreach(ObjectPropertyContainer iobj in importedObjects)
    {
        if (iobj is null) { continue; }
        UndertaleGameObject obj;
        obj = Data.GameObjects.ByName(iobj.Name);
        obj.Visible = iobj.isVisible;
        obj.Persistent = iobj.isPersistent;
        UndertaleSprite objSprite = null;
        if (iobj.Sprite is not null) { objSprite = Data.Sprites.ByName(iobj.Sprite); }
        obj.Sprite = objSprite;
        UndertaleGameObject pobj = null;
        if (iobj.parentObject is not null)
            pobj = Data.GameObjects.ByName(iobj.parentObject);
        if (pobj is not null)
            obj.ParentId = pobj;
        UndertaleSprite objmask = null;
        if (iobj.textureMask is not null)
            objmask = Data.Sprites.ByName(iobj.textureMask);
        if (objmask is not null)
            obj.TextureMaskId = objmask;
        obj.CollisionShape = iobj.collisionShape;
        UndertalePointerList<UndertalePointerList<UndertaleGameObject.Event>> newEvents = new UndertalePointerList<UndertalePointerList<UndertaleGameObject.Event>>();
        foreach (EventList ievnt in iobj.Events)
        {
            UndertaleGameObject.Event evnt = new UndertaleGameObject.Event();
            evnt.EventSubtype = ievnt.subtype;
            if ((ievnt.evtype == (int)EventType.Collision) && ievnt.collisionType is not null)
            {
                UndertaleGameObject collisionObj = Data.GameObjects.ByName(ievnt.collisionType);
                if (collisionObj is not null) { evnt.EventSubtype = (uint)Data.GameObjects.IndexOf(collisionObj); }
            }
            foreach (string actioncode in ievnt.eventCode)
            {
                UndertaleGameObject.EventAction evntAction = new UndertaleGameObject.EventAction();
                UndertaleCode assignedCode = null;
                assignedCode = Data.Code.ByName(actioncode);
                evntAction.CodeId = assignedCode;
                evnt.Actions.Add(evntAction);
            }
            UndertaleGameObject.Event currentEvent = obj.Events[ievnt.evtype].Where((x) => x.EventSubtype == 0).FirstOrDefault();
            if (currentEvent is not null)
                currentEvent = evnt;
            else
            {
                obj.Events[ievnt.evtype].Add(evnt);
            }
        }
        IncrementProgressParallel();
    };
    if (importedRooms.Count > 0) { SetProgressBar(null, "Importing Rooms", 0, importedRooms.Count); }
    foreach (RoomPropertyContainer irm in importedRooms)
    {
        if (irm is null) { continue; }
        UndertaleRoom room;
        room = Data.Rooms.ByName(irm.Name);
        room.Backgrounds.Clear();
        room.Views.Clear();
        room.GameObjects.Clear();
        room.Tiles.Clear();
        room.Layers.Clear();
        room.GridHeight = 32;
        room.GridWidth = 32;
        room.GridThicknessPx = 1;
        room.BackgroundColor = 0;
        room.DrawBackgroundColor = false;
        UndertaleCode roomCC = null;
        roomCC = Data.Code.ByName(irm.RoomCC);
        if (roomCC != null) { room.CreationCodeId = roomCC; }
        room.Width = irm.Size[0]; room.Height = irm.Size[1];
        room.Top = 0; room.Bottom = 0; room.Left = 0; room.Right = 0;
        room.Caption = null;
        if (irm.Caption is not null && irm.Caption.Length > 0) { room.Caption = Data.Strings.MakeString(irm.Caption); }
        room.World = false;
        room.GravityX = irm.Gravity[0]; room.GravityY = irm.Gravity[1];
        room.MetersPerPixel = 0.1f;
        room.Speed = irm.Speed;
        room.Flags = irm.Flags;
        foreach (ViewPropertyContainer iview in irm.Views)
        {
            if (iview is null) { continue; }
            bool viewEnabled = false;
            viewEnabled = iview.isEnabled;
            UndertaleRoom.View view = new UndertaleRoom.View()
            {
                ViewX = iview.Pos[0],
                ViewY = iview.Pos[1],
                ViewWidth = iview.Size[0],
                ViewHeight = iview.Size[1],
                PortX = iview.PortPos[0],
                PortY = iview.PortPos[1],
                PortWidth = iview.PortSize[0],
                PortHeight = iview.PortSize[1],
                SpeedX = iview.Speed[0],
                SpeedY = iview.Speed[1],
                BorderX = iview.Border[0],
                BorderY = iview.Border[1],
                ObjectId = Data.GameObjects.ByName(iview.Object),
                Enabled = viewEnabled
            };
            room.Views.Add(view);
        }
        foreach (LayerPropertyContainer ilayer in irm.Layers)
        {
            if (ilayer is null) { continue; }
            UndertaleRoom.Layer lay = new UndertaleRoom.Layer()
            {
                ParentRoom = room,
                LayerName = Data.Strings.MakeString(ilayer.Name),
                LayerType = ilayer.LayerType,
                LayerId = ilayer.ID,
                IsVisible = ilayer.isVisible,
                LayerDepth = ilayer.Depth,
                HSpeed = ilayer.Speed[0],
                VSpeed = ilayer.Speed[1],
                XOffset = ilayer.Offset[0],
                YOffset = ilayer.Offset[1],
            };
            switch (lay.LayerType)
            {
                case LayerType.Background:
                    UndertaleSprite bgSpr = null;
                    if (ilayer.Sprite is not null) { bgSpr = Data.Sprites.ByName(ilayer.Sprite); }
                    UndertaleRoom.Layer.LayerBackgroundData bgData = new UndertaleRoom.Layer.LayerBackgroundData()
                    {
                        ParentLayer = lay,
                        Visible = lay.IsVisible,
                        Foreground = false,
                        TiledHorizontally = ilayer.isTiled[0],
                        TiledVertically = ilayer.isTiled[1],
                        Stretch = ilayer.isStretched,
                        Color = uint.Parse(ilayer.Color, System.Globalization.NumberStyles.HexNumber),
                        FirstFrame = 0,
                        AnimationSpeed = ilayer.AnimSpeed,
                        AnimationSpeedType = ilayer.AnimType,
                        Sprite = bgSpr
                    };
                    lay.Data = bgData;
                    break;
                case LayerType.Instances:
                    UndertaleRoom.Layer.LayerInstancesData objData = new UndertaleRoom.Layer.LayerInstancesData();
                    if (ilayer.Instances is null) { lay.Data = objData; break; }
                    foreach (RoomInstance iobj in ilayer.Instances)
                    {
                        if (iobj is null || iobj.Definition is null) { continue; }
                        try
                        {
                            UndertaleRoom.GameObject obj = new UndertaleRoom.GameObject()
                            {
                                X = iobj.Position[0],
                                Y = iobj.Position[1],
                                InstanceID = iobj.InstanceID,
                                ObjectDefinition = Data.GameObjects.ByName(iobj.Definition),
                                CreationCode = Data.Code.ByName(iobj.RoomCC),
                                PreCreateCode = Data.Code.ByName(iobj.RoomPCC),
                                ScaleX = iobj.Scale[0],
                                ScaleY = iobj.Scale[1],
                                Color = uint.Parse(iobj.Color, System.Globalization.NumberStyles.HexNumber),
                                Rotation = iobj.Rotation,
                                ImageIndex = 0,
                                ImageSpeed = iobj.ImgSpeed
                            };
                            objData.Instances.Add(obj);
                            room.GameObjects.Add(obj);
                        }
                        catch
                        {
                            ScriptMessage($"Failed to place object {iobj.Definition} in room {room.Name.Content}");
                        }
                        
                    }
                    lay.Data = objData;
                    break;
                case LayerType.Tiles:
                    uint[][] tileData = new uint[ilayer.TileDataSize[0]][];
                    for (int i = 0; i < ilayer.TileDataSize[0]; i++)
                    {
                        tileData[i] = new uint[ilayer.TileDataSize[1]];
                    }
                    if (ilayer.TileData is not null && ilayer.TileData.Length > 0 && ilayer.TileData != "")
                    {
                        string[] tileRows = ilayer.TileData.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).Select(row => row.Replace(" ", "")).ToArray(); //spaces in tiledata are haunting me
                        tileData = tileRows.Select(row => row.Split(';').Select(uint.Parse).ToArray()).ToArray(); //more unreadable slop but it works
                    }
                    UndertaleRoom.Layer.LayerTilesData tilesData = new UndertaleRoom.Layer.LayerTilesData()
                    {
                        TilesX = ilayer.TileDataSize[0],
                        TilesY = ilayer.TileDataSize[1],
                        Background = Data.Backgrounds.ByName(ilayer.Tileset),
                        TileData = tileData
                    };
                    lay.Data = tilesData;
                    break;
                case LayerType.Assets:
                    UndertaleRoom.Layer.LayerAssetsData assetsData = new UndertaleRoom.Layer.LayerAssetsData();
                    assetsData.Sequences = new UndertalePointerList<SequenceInstance>();
                    assetsData.LegacyTiles = new UndertalePointerList<Tile>();
                    assetsData.Sequences = new UndertalePointerList<SequenceInstance>();
                    assetsData.Sprites = new UndertalePointerList<SpriteInstance>();
                    foreach (RoomSprite asset in ilayer.Sprites)
                    {
                        try
                        {
                            if (asset is null) { continue; }
                            UndertaleRoom.SpriteInstance spr = new UndertaleRoom.SpriteInstance()
                            {
                                Sprite = Data.Sprites.ByName(asset.sprite),
                                Name = Data.Strings.MakeString(asset.name),
                                X = asset.Position[0],
                                Y = asset.Position[1],
                                ScaleX = asset.Scale[0],
                                ScaleY = asset.Scale[1],
                                Color = uint.Parse(asset.color, System.Globalization.NumberStyles.HexNumber),
                                AnimationSpeed = asset.animSpeed,
                                AnimationSpeedType = asset.animType,
                                FrameIndex = asset.frameIndex,
                                Rotation = asset.rotation
                            };
                            assetsData.Sprites.Add(spr);
                        }
                        catch
                        {
                            ScriptMessage($"Failed to import sprite asset in room {room.Name.Content}!");
                        }
                    }
                    lay.Data = assetsData;
                    break;
            }
            room.Layers.Add(lay);
        }
        IncrementProgressParallel();
    };
    ScriptMessage("Import Complete!");
}
async Task DoComparison()
{
    if (getSprites == true)
    {
        SetProgressBar(null, "Comparing Sprites", 0, Data.Sprites.Count);
        await CompareSprites();
        TextureWorker worker = new TextureWorker();
        SetProgressBar(null, "Exporting Sprites", 0, ExportSprites.Count);
        await Task.Run(() => Parallel.ForEach(ExportSprites, new ParallelOptions { MaxDegreeOfParallelism = 4 }, sprite =>
        {
            if (ignoreList.Sprites.Contains(sprite.Name.Content)) { return; }
            int[] sorigin = { sprite.OriginX, sprite.OriginY};
            int[] smargin = { sprite.MarginLeft, sprite.MarginRight, sprite.MarginBottom, sprite.MarginTop};
            SpritePropertyContainer sProperties = new SpritePropertyContainer()
            {
                Name = sprite.Name.Content,
                BBMode = sprite.BBoxMode,
                PlaybackSpeed = sprite.GMS2PlaybackSpeed,
                PlaybackType = sprite.GMS2PlaybackSpeedType,
                Height = sprite.Height,
                Width = sprite.Width,
                Origin = sorigin,
                Margin = smargin,
                isSpecial = sprite.IsSpecialType,
                SpecialVersion = sprite.SVersion,
                Transparent = sprite.Transparent,
                SMaskType = sprite.SepMasks,
                isSmooth = sprite.Smooth,
                isPreload = sprite.Preload,
                CMask = (sprite.CollisionMasks.Count > 0)
            };
            string spriteDir = projectFolder + @"\sprites\" + sprite.Name.Content + @"\";
            Directory.CreateDirectory(spriteDir);
            for (int i = 0; i < sprite.Textures.Count; i++)
            {
                if (sprite.Textures[i]?.Texture != null)
                {
                    try
                    {
                        worker.ExportAsPNG(sprite.Textures[i].Texture, spriteDir + sprite.Name.Content + "_" + i + ".png", null, true);
                    }
                    catch (Exception ex)
                    {
                        ScriptMessage($"There was a problem during the export of a sprite frame!\n{ex}");
                    }
                }
            }
            string spriteJSON = JsonConvert.SerializeObject(sProperties, Formatting.Indented);
            System.IO.File.WriteAllText(spriteDir + sprite.Name.Content + @"_properties.json", spriteJSON);
            IncrementProgressParallel();

        }));
        SetProgressBar(null, "Comparing Tilesets", 0, Data.Backgrounds.Count);
        await CompareTilesets();
        SetProgressBar(null, "Exporting Tilesets", 0, ExportTilesets.Count);
        await Task.Run(() => Parallel.ForEach(ExportTilesets, new ParallelOptions { MaxDegreeOfParallelism = 4 }, tileset => 
        {
            if (ignoreList.Tilesets.Contains(tileset.Name.Content)) { return; }
            string tilesetDir = projectFolder + @"\tilesets\" + tileset.Name.Content + @"\";
            Directory.CreateDirectory(tilesetDir);
            TilesetPropertyContainer tsProp = new TilesetPropertyContainer() 
            { 
                Name = tileset.Name.Content,
                TileHeight = tileset.GMS2TileHeight,
                TileWidth = tileset.GMS2TileWidth,
                TileColumns = tileset.GMS2TileColumns,
                TileCount = tileset.GMS2TileCount,
                ItemsPerTile = tileset.GMS2ItemsPerTileCount,
                FrameTime = tileset.GMS2FrameLength,
                BorderX = tileset.GMS2OutputBorderX,
                BorderY = tileset.GMS2OutputBorderY
            };
            worker.ExportAsPNG(tileset.Texture, tilesetDir + tileset.Name.Content + ".png", null, true);
            string tilesetJSON = JsonConvert.SerializeObject(tsProp, Formatting.Indented);
            System.IO.File.WriteAllText(tilesetDir + tileset.Name.Content + @"_properties.json", tilesetJSON);
            IncrementProgressParallel();
        }));
    }
    if (getObjects == true)
    {
        SetProgressBar(null, "Comparing Objects", 0, Data.GameObjects.Count);
        await CompareObjects();
        string objectDir = projectFolder + @"\objects\";
        Directory.CreateDirectory(objectDir);
        SetProgressBar(null, "Comparing Objects", 0, ExportObjects.Count);
        await Task.Run(() => Parallel.ForEach(ExportObjects, new ParallelOptions { MaxDegreeOfParallelism = 8 }, obj =>
        {
            if (ignoreList.Objects.Contains(obj.Name.Content)) { return; }
            string ospr = null;
            if (obj.Sprite is not null) { ospr = obj.Sprite.Name.Content; }
            ObjectPropertyContainer oProperties = new ObjectPropertyContainer()
            {
                Name = obj.Name.Content,
                Sprite = ospr,
                isVisible = obj.Visible,
                isSolid = obj.Solid,
                isPersistent = obj.Persistent,
                parentObject = (obj.ParentId is null ? "" : obj.ParentId.Name.Content),
                textureMask = (obj.TextureMaskId is null ? "" : obj.TextureMaskId.Name.Content),
                collisionShape = obj.CollisionShape,
                Events = new List<EventList>()
            };
            for (var i = 0; i < obj.Events.Count; i++)
            {
                foreach (UndertaleGameObject.Event evnt in obj.Events[i])
                {
                    EventList eventList = new EventList()
                    {
                        evtype = i,
                        subtype = evnt.EventSubtype,
                        eventCode = new List<string>(),
                        collisionType = null
                   };
                    if (i == (int)EventType.Collision)
                    {
                        eventList.collisionType = Data.GameObjects[(int)evnt.EventSubtype].Name.Content;
                    }
                    foreach (UndertaleGameObject.EventAction action in evnt.Actions)
                    {
                        try { eventList.eventCode.Add(action.CodeId.Name.Content); }
                        catch { eventList.eventCode.Add(""); }
                    }
                    oProperties.Events.Add(eventList);
                }
            }
            string objectJSON = JsonConvert.SerializeObject(oProperties, Formatting.Indented);
            System.IO.File.WriteAllText(objectDir + obj.Name.Content + @"_properties.json", objectJSON);
            IncrementProgressParallel();
        }));
    }
    if (getCode == true)
    {
        SetProgressBar(null, "Comparing Code", 0, Data.Code.Count);
        await CompareCode();
        string codeDir = projectFolder + @"code\";
        Directory.CreateDirectory(codeDir);
        ThreadLocal<GlobalDecompileContext> DECOMPILE_CONTEXT = new ThreadLocal<GlobalDecompileContext>(() => new GlobalDecompileContext(Data, false));
        if (Data.KnownSubFunctions is null)
        {
            Decompiler.BuildSubFunctionCache(Data);
            GenerateGMLCache(DECOMPILE_CONTEXT);
        }
        string functionDir = Path.Combine(projectFolder, "functions");
        Directory.CreateDirectory(functionDir);
        string functionJSON = $"[\n";
        foreach (UndertaleCode func in ExportFunctions)
        {
            if (ignoreList.Functions.Contains(func.Name.Content) || ignoreList.Code.Contains(func.ParentEntry.Name.Content)) { continue; }
            if (functionJSON != $"[\n")
                functionJSON += $",\n";
            functionJSON += $"  {{\n";
            functionJSON += $"    \"Name\": \"{func.Name.Content}\",\n";
            functionJSON += $"    \"Code\": \"{func.ParentEntry.Name.Content}\"\n";
            functionJSON += $"  }}";
        }
        functionJSON += $"\n]\n";
        System.IO.File.WriteAllText(functionDir + @"\functions.json", functionJSON);
        SetProgressBar(null, "Exporting Code", 0, ExportCode.Count);
        await Task.Run(() => Parallel.ForEach(ExportCode, new ParallelOptions { MaxDegreeOfParallelism = 8 }, code =>
        {
            IncrementProgressParallel();
            if (ignoreList.Code.Contains(code.Name.Content)) { return; }
            string codestr = null;
            try
            {
                codestr = Decompiler.Decompile(code, DECOMPILE_CONTEXT.Value);
                System.IO.File.WriteAllText(codeDir + code.Name.Content + @".gml", codestr);
            }
            catch
            {
                codestr = code.Disassemble(Data.Variables, Data.CodeLocals.For(code));
                System.IO.File.WriteAllText(codeDir + code.Name.Content + @".asm", codestr);
            }
        }));
        await CompareScripts();
        string scriptDir = Path.Combine(projectFolder, "scripts");
        Directory.CreateDirectory(scriptDir);
        string scriptJSON = $"[\n";
        foreach (UndertaleScript script in ExportScript)
        {
            if (ignoreList.Scripts.Contains(script.Name.Content)) { continue; }
            if (scriptJSON != $"[\n")
                scriptJSON += $",\n";
            scriptJSON += $"  {{\n";
            scriptJSON += $"    \"Name\": \"{script.Name.Content}\",\n";
            scriptJSON += $"    \"Code\": \"{script.Code.Name.Content}\"\n";
            scriptJSON += $"  }}";
        }
        scriptJSON += $"\n]\n";
        System.IO.File.WriteAllText(scriptDir + @"\scripts.json", scriptJSON);
    }
    if (getRooms == true)
    {
        SetProgressBar(null, "Comparing Rooms", 0, Data.Rooms.Count);
        await CompareRooms();
        string roomDir = projectFolder + @"\rooms\";
        Directory.CreateDirectory(roomDir);
        SetProgressBar(null, "Exporting Rooms", 0, ExportRooms.Count);
        await Task.Run(() => Parallel.ForEach(ExportRooms, new ParallelOptions { MaxDegreeOfParallelism = 4 }, room => 
        {
            IncrementProgressParallel();
            if (ignoreList.Rooms.Contains(room.Name.Content)) { return; }
            uint[] size = {room.Width, room.Height};
            float[] grav = {room.GravityX, room.GravityY};
            string cap = null;
            if (room.Caption is not null) { cap = room.Caption.Content; }
            string rcc = null;
            if (room.CreationCodeId is not null) { rcc = room.CreationCodeId?.Name.Content; }
            RoomPropertyContainer roomProperties = new RoomPropertyContainer()
            {
                Layers = new List<LayerPropertyContainer>(),
                Views = new List<ViewPropertyContainer>(),
                Name = room.Name.Content,
                RoomCC = rcc,
                Size = size,
                Flags = room.Flags,
                Gravity = grav,
                Caption = cap,
                Speed = room.Speed
            };
            Parallel.ForEach(room.Views, new ParallelOptions { MaxDegreeOfParallelism = 8 }, view =>
            {
                int[] vpos = { view.ViewX, view.ViewY };
                int[] vsize = { view.ViewWidth, view.ViewHeight };
                int[] vportpos = { view.PortX, view.PortY };
                int[] vportsize = { view.PortWidth, view.PortHeight };
                uint[] vborder = { view.BorderX, view.BorderY };
                int[] vspeed = { view.SpeedX, view.SpeedY };
                string vobject = view.ObjectId?.Name.Content;
                ViewPropertyContainer viewProperties = new ViewPropertyContainer()
                {
                    Pos = vpos,
                    Size = vsize,
                    PortPos = vportpos,
                    PortSize = vportsize,
                    Border = vborder,
                    Speed = vspeed,
                    Object = vobject
                };
                roomProperties.Views.Add(viewProperties);
            });
            if (room.Layers is not null)
            {
                try
                {
                    Parallel.ForEach(room.Layers, new ParallelOptions { MaxDegreeOfParallelism = 4 }, lay =>
                    {
                        uint[] tilesSize = null;
                        string tileData = null;
                        float[] layOffset = { lay.XOffset, lay.YOffset };
                        float[] laySpeed = { lay.HSpeed, lay.VSpeed };
                        bool[] layTiling = null;
                        string bgSpr = null;
                        float aspeed = 30;
                        uint laycolor = Convert.ToUInt32("FFFFFFFF", 16);
                        bool laystretched = false;
                        string tileset = null;
                        AnimationSpeedType aspeedtype = AnimationSpeedType.FPS;

                        if (lay.TilesData is not null)
                        {
                            uint[] ctilesSize = { lay.TilesData.TilesX, lay.TilesData.TilesY };
                            tilesSize = ctilesSize;
                            tileset = lay?.TilesData?.Background?.Name.Content;
                            if (lay.TilesData.TileData.Length > 0)
                            {
                                StringBuilder sb = new();
                                foreach (uint[] dataRow in lay.TilesData.TileData)
                                    sb.AppendLine(String.Join(";", dataRow.Select(x => x.ToString())));
                                tileData = sb.ToString();
                            }
                        }
                        if (lay.BackgroundData is not null)
                        {
                            bool[] laytiling = { lay.BackgroundData.TiledHorizontally, lay.BackgroundData.TiledVertically };
                            layTiling = laytiling;
                            if (lay.BackgroundData.Sprite is not null)
                                bgSpr = lay.BackgroundData.Sprite.Name.Content;
                            aspeed = lay.BackgroundData.AnimationSpeed;
                            aspeedtype = lay.BackgroundData.AnimationSpeedType;
                            laycolor = lay.BackgroundData.Color;
                            laystretched = lay.BackgroundData.Stretch;
                        }
                        LayerPropertyContainer layProperties = new LayerPropertyContainer()
                        {
                            Name = lay.LayerName.Content,
                            LayerType = lay.LayerType,
                            ID = lay.LayerId,
                            Instances = new List<RoomInstance>(),
                            isVisible = lay.IsVisible,
                            Depth = lay.LayerDepth,
                            Tileset = tileset,
                            TileData = tileData,
                            TileDataSize = tilesSize,
                            AnimSpeed = aspeed,
                            AnimType = aspeedtype,
                            isTiled = layTiling,
                            isStretched = laystretched,
                            Color = laycolor.ToString("X"),
                            Offset = layOffset,
                            Speed = laySpeed,
                            Sprite = bgSpr
                        };
                        if (lay.LayerType == LayerType.Instances)
                        {
                            Parallel.ForEach(lay.InstancesData.Instances, new ParallelOptions { MaxDegreeOfParallelism = 8 }, inst =>
                            {
                                int[] pos = { inst.X, inst.Y };
                                float[] scale = { inst.ScaleX, inst.ScaleY };
                                string cc = null;
                                string pcc = null;
                                if (inst.CreationCode is not null) { cc = inst?.CreationCode.Name.Content; }
                                if (inst.PreCreateCode is not null) { pcc = inst?.PreCreateCode.Name.Content; }
                                RoomInstance eInst = new RoomInstance()
                                {
                                    Definition = inst.ObjectDefinition.Name.Content,
                                    InstanceID = inst.InstanceID,
                                    Position = pos,
                                    Scale = scale,
                                    RoomCC = cc,
                                    RoomPCC = pcc,
                                    Color = inst.Color.ToString("X"),
                                    Rotation = inst.Rotation,
                                    ImgSpeed = inst.ImageSpeed
                                };
                                layProperties.Instances.Add(eInst);
                            });
                        }
                        else if (lay.LayerType == LayerType.Assets)
                        {
                            layProperties.Sprites = new List<RoomSprite>();
                            Parallel.ForEach(lay.AssetsData.Sprites, new ParallelOptions { MaxDegreeOfParallelism = 4 }, asset =>
                            {
                                if (asset is null) { return; }
                                int[] pos = { asset.X, asset.Y };
                                float[] scale = { asset.ScaleX, asset.ScaleY };
                                RoomSprite roomSprite = new RoomSprite()
                                {
                                    sprite = asset.Sprite.Name.Content,
                                    name = asset.Name.Content,
                                    Position = pos,
                                    Scale = scale,
                                    color = asset.Color.ToString("X"),
                                    animSpeed = asset.AnimationSpeed,
                                    animType = asset.AnimationSpeedType,
                                    frameIndex = asset.FrameIndex,
                                    rotation = asset.Rotation
                                };
                                layProperties.Sprites.Add(roomSprite);
                            });
                        }
                        roomProperties.Layers.Add(layProperties);
                    });
                }
                catch
                {
                    ScriptMessage($"Unable to Export Layer for Room {room.Name.Content}");
                }
            }
            string roomJSON = JsonConvert.SerializeObject(roomProperties, Formatting.Indented);
            System.IO.File.WriteAllText(roomDir + room.Name.Content + @".json", roomJSON);
        }));
    }
    ScriptMessage("Export Complete!");
}

#region Containers
public class IgnoreList
{
    public List<string> Code { get; set; }
    public List<string> Scripts { get; set; }
    public List<string> Functions { get; set; }
    public List<string> Sprites { get; set; }
    public List<string> Tilesets { get; set; }
    public List<string> Rooms { get; set; }
    public List<string> Objects { get; set; }
    public IgnoreList()
    {
        Code = new List<string>();
        Scripts = new List<string>();
        Sprites = new List<string>();
        Tilesets = new List<string>();
        Rooms = new List<string>();
        Objects = new List<string>();
        Functions = new List<string>();
    }
}
public class SpritePropertyContainer
{
    public string Name { get; set; }
    public bool CMask { get; set; }
    public uint BBMode { get; set; }
    public float PlaybackSpeed { get; set; }
    public AnimSpeedType PlaybackType { get; set; }
    public uint Height { get; set; }
    public uint Width { get; set; }
    public int[] Origin { get; set; }
    public int[] Margin { get; set; }
    public bool isSpecial { get; set; }
    public uint SpecialVersion { get; set; }
    public bool Transparent { get; set; }
    public UndertaleSprite.SepMaskType SMaskType { get; set; }
    public bool isSmooth { get; set; }
    public bool isPreload { get; set; }
}

public class TilesetPropertyContainer
{
    public string Name { get; set; }
    public uint TileHeight { get; set; }
    public uint TileWidth { get; set; }
    public uint TileColumns { get; set; }
    public uint TileCount { get; set; }
    public uint ItemsPerTile { get; set; }
    public long FrameTime { get; set; }
    public uint BorderX { get; set; }
    public uint BorderY { get; set; }
}
public class ObjectPropertyContainer
{
    public string Name { get; set; }
    public string Sprite { get; set; }
    public bool isVisible { get; set; }
    public bool isSolid { get; set; }
    public bool isPersistent { get; set; }
    public string parentObject { get; set; }
    public string textureMask { get; set; }
    public CollisionShapeFlags collisionShape { get; set; }
    public List<EventList> Events { get; set; }
}
public class EventList
{
    public int evtype { get; set; }
    public uint subtype { get; set; }
    public List<string> eventCode { get; set; }
    public string collisionType { get; set; }
}

public class RoomPropertyContainer
{
    public List<LayerPropertyContainer> Layers { get; set; }
    public List<ViewPropertyContainer> Views { get; set; }
    public string Name { get; set; }
    public string RoomCC { get; set; }
    public uint[] Size { get; set; }
    public string Caption { get; set; }
    public float[] Gravity { get; set; }
    public uint Speed { get; set; }
    public UndertaleRoom.RoomEntryFlags Flags { get; set; }

}

public class LayerPropertyContainer
{
    public UndertaleRoom.LayerType LayerType { get; set; }
    public List<RoomInstance> Instances { get; set; }
    public List<RoomSprite> Sprites { get; set; }
    public string Name { get; set; }
    public bool isVisible { get; set; }
    public int Depth { get; set; }
    public uint[] TileDataSize { get; set; }
    public string Sprite { get; set; }
    public string Tileset { get; set; }
    public string TileData { get; set; }
    public bool[] isTiled { get; set; }
    public bool isStretched { get; set; }
    public string Color { get; set; }
    public float AnimSpeed { get; set; }
    public AnimationSpeedType AnimType { get; set; }
    public float[] Offset { get; set; }
    public float[] Speed { get; set; }
    public uint ID { get; set; }

}

public class ViewPropertyContainer
{
    public int[] Pos { get; set; }
    public int[] Size { get; set; }
    public int[] PortPos { get; set; }
    public int[] PortSize { get; set; }
    public uint[] Border { get; set; }
    public int[] Speed { get; set; }
    public string Object { get; set; }
    public bool isEnabled { get; set; }
}

public class RoomInstance
{
    public int[] Position { get; set; }
    public string Definition { get; set; }
    public uint InstanceID { get; set; }
    public string RoomCC { get; set; }
    public float[] Scale { get; set; }
    public string Color { get; set; }
    public float Rotation { get; set; }
    public string RoomPCC { get; set; }
    public float ImgSpeed { get; set; }
}

public class RoomSprite
{
    public string sprite { get; set; }
    public string name { get; set; }
    public int[] Position { get; set; }
    public float[] Scale { get; set; }
    public string color { get; set; }
    public float animSpeed { get; set; }
    public AnimationSpeedType animType { get; set; }
    public float frameIndex { get; set; }
    public float rotation { get; set; }


}
#endregion

bool makeCollisionBox(string source)
{
    string path = Path.GetDirectoryName(source);
    string jsonFile = Directory.GetFiles(path, $"*.json", SearchOption.AllDirectories).FirstOrDefault();
    string json = File.ReadAllText(jsonFile);
    SpritePropertyContainer spriteProperties = JsonConvert.DeserializeObject<SpritePropertyContainer>(json);
    if (spriteProperties != null )
        return spriteProperties.CMask;
    else { return false; }
}

#region Comparisons
async Task CompareSprites()
{
    await Task.Run(() => Parallel.ForEach(Data.Sprites, new ParallelOptions { MaxDegreeOfParallelism = (comparePixels ? 2 : 4) }, Sprite =>
    {
        IncrementProgressParallel();
        if (ignoreList.Sprites.Contains(Sprite.Name.Content)) { return; }
        UndertaleSprite msprite = Sprite as UndertaleSprite;
        UndertaleSprite spritematch = null;
        spritematch = vData.Sprites.ByName(msprite.Name.Content);
        if (spritematch is null || spritematch.OriginX != msprite.OriginX || spritematch.OriginY != msprite.OriginY || spritematch.Height != msprite.Height || spritematch.Width != msprite.Width)
        {
            ExportSprites.Add(msprite);
            return;
        }
        else if (spritematch.Textures.Count != msprite.Textures.Count)
        {
            ExportSprites.Add(msprite);
            return;
        }
        else
        {
            for (int i = 0; i < spritematch.Textures.Count; i++) 
            { 
                if (spritematch.Textures[i].Texture.Name.Content != msprite.Textures[i].Texture.Name.Content)
                {
                    ExportSprites.Add(msprite);
                    return;
                }
                else if (comparePixels == true) //oh lord
                {
                    Bitmap vMap = MakePIBitmap(spritematch.Textures[i].Texture);
                    Bitmap mMap = MakePIBitmap(msprite.Textures[i].Texture);
                    if (vMap != null &&  mMap != null) 
                    {
                        if (CompareBitmaps(vMap, mMap) == false)
                        {
                            vMap.Dispose(); mMap.Dispose();
                            ExportSprites.Add(msprite);
                            return;
                        }
                    }
                    vMap.Dispose(); mMap.Dispose();
                }
            }
        };
    }));
}
bool CompareBitmaps(Bitmap bmp1, Bitmap bmp2)
{
    if (bmp1 == null || bmp2 == null)
        return false;
    if (object.Equals(bmp1, bmp2))
        return true;
    if (!bmp1.Size.Equals(bmp2.Size) || !bmp1.PixelFormat.Equals(bmp2.PixelFormat))
        return false;

    int bytes = bmp1.Width * bmp1.Height * (Image.GetPixelFormatSize(bmp1.PixelFormat) / 8);

    bool result = true;
    byte[] b1bytes = new byte[bytes];
    byte[] b2bytes = new byte[bytes];

    BitmapData bitmapData1 = bmp1.LockBits(new Rectangle(0, 0, bmp1.Width, bmp1.Height), ImageLockMode.ReadOnly, bmp1.PixelFormat);
    BitmapData bitmapData2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width, bmp2.Height), ImageLockMode.ReadOnly, bmp2.PixelFormat);
    Marshal.Copy(bitmapData1.Scan0, b1bytes, 0, bytes);
    Marshal.Copy(bitmapData2.Scan0, b2bytes, 0, bytes);

    for (int n = 0; n <= bytes - 1; n++)
    {
        if (b1bytes[n] != b2bytes[n])
        {
            result = false;
            break;
        }
    }

    bmp1.UnlockBits(bitmapData1);
    bmp2.UnlockBits(bitmapData2);

    return result;
}
Bitmap MakePIBitmap(UndertaleTexturePageItem texPageItem)
{
    TextureWorker bmworker = new TextureWorker();
    int exportWidth = texPageItem.BoundingWidth; // sprite.Width
    int exportHeight = texPageItem.BoundingHeight; // sprite.Height
    Bitmap embeddedImage = bmworker.GetEmbeddedTexture(texPageItem.TexturePage);

    // Sanity checks.
    if ((texPageItem.TargetWidth > exportWidth) || (texPageItem.TargetHeight > exportHeight))
        throw new InvalidDataException("the texture is larger than its bounding box!");

    // Create a bitmap representing that part of the texture page.
    Bitmap resultImage = null;
    lock (embeddedImage)
    {
        try
        {
            resultImage = embeddedImage.Clone(new Rectangle(texPageItem.SourceX, texPageItem.SourceY, texPageItem.SourceWidth, texPageItem.SourceHeight), 0);
        }
        catch (OutOfMemoryException)
        {
            throw new OutOfMemoryException("the texture is abnormal. 'Source Position/Size' boxes 3 & 4 on texture page may be bigger than the sprite itself or it's set to '0'.");
        }
    }
    
    // Resize the image, if necessary.
    if ((texPageItem.SourceWidth != texPageItem.TargetWidth) || (texPageItem.SourceHeight != texPageItem.TargetHeight))
        resultImage = TextureWorker.ResizeImage(resultImage, texPageItem.TargetWidth, texPageItem.TargetHeight);

    // Put it in the final holder image.
    Bitmap returnImage = resultImage;
    embeddedImage.Dispose();
    bmworker.Cleanup();
    return returnImage;
}
async Task CompareTilesets()
{
    await Task.Run(() => Parallel.ForEach(Data.Backgrounds, new ParallelOptions { MaxDegreeOfParallelism = 4 }, tileset =>
    {
        IncrementProgressParallel();
        if (ignoreList.Tilesets.Contains(tileset.Name.Content)) { return; }
        UndertaleBackground mTS = tileset as UndertaleBackground;
        UndertaleBackground TSMatch = null;
        TSMatch = vData.Backgrounds.ByName(mTS.Name.Content);
        if (TSMatch is null || TSMatch.GMS2TileColumns != mTS.GMS2TileColumns || TSMatch.GMS2TileWidth != mTS.GMS2TileWidth || TSMatch.GMS2TileHeight != mTS.GMS2TileHeight || TSMatch.GMS2TileCount != mTS.GMS2TileCount || TSMatch.GMS2ItemsPerTileCount != mTS.GMS2ItemsPerTileCount || TSMatch.GMS2FrameLength != mTS.GMS2FrameLength || TSMatch.GMS2OutputBorderX != mTS.GMS2OutputBorderX || TSMatch.GMS2OutputBorderY != mTS.GMS2OutputBorderY)
        {
            ExportTilesets.Add(mTS);
            return;
        }
        else if (TSMatch.Texture.Name.Content !=  mTS.Texture.Name.Content)
        {
            ExportTilesets.Add(mTS);
            return;
        }
    }));
}
async Task CompareObjects()
{
    await Task.Run(() => Parallel.ForEach(Data.GameObjects, new ParallelOptions { MaxDegreeOfParallelism = 8 }, Object =>
    {
        IncrementProgressParallel();
        if (ignoreList.Objects.Contains(Object.Name.Content)) { return; }
        UndertaleGameObject mObject = Object as UndertaleGameObject;
        UndertaleGameObject objectMatch = null;
        objectMatch = vData.GameObjects.ByName(mObject.Name.Content);
        if (objectMatch is null || objectMatch.Visible != mObject.Visible || objectMatch.Events.Count != mObject.Events.Count)
        {
            ExportObjects.Add(mObject);
            return;
        }
        if (objectMatch.Sprite is not null || mObject.Sprite is not null)
        {
            if (objectMatch.Sprite is null || mObject.Sprite is null)
            {
                ExportObjects.Add(mObject);
                return;
            }
            else if (objectMatch.Sprite.Name.Content != mObject.Sprite.Name.Content)
            {
                ExportObjects.Add(mObject);
                return;
            }
        }
        if (objectMatch.ParentId is not null || mObject.ParentId is not null) //this section is terrible
        {
            if (objectMatch.ParentId is null || mObject.ParentId is null)
            {
                ExportObjects.Add(mObject);
                return;
            }
            else if (objectMatch.ParentId.Name.Content != mObject.ParentId.Name.Content)
            {
                ExportObjects.Add(mObject);
                return;
            }
        }
        EventType[] EvTypes = (EventType[])Enum.GetValues(typeof(EventType));
        for (var i = 0; i < mObject.Events.Count; i++)
        {
            foreach(UndertaleGameObject.Event evnt in mObject.Events[i])
            {
                EventType evType = EvTypes[i];
                uint subType = evnt.EventSubtype;
                try
                {
                    Event vevnt = objectMatch.Events[i].Where((x) => x.EventSubtype == subType).FirstOrDefault();
                    if (vevnt is null)
                    {
                        ExportObjects.Add(mObject);
                        return;
                    }
                    if (evnt.Actions.Count != vevnt.Actions.Count)
                    {
                        ExportObjects.Add(mObject);
                        return;
                    }
                    for (var j = 0; j < evnt.Actions.Count; j++)
                    {
                        UndertaleCode modCode = null;
                        UndertaleCode matchCode = null;
                        modCode = evnt.Actions[j].CodeId;
                        matchCode = vevnt.Actions[j].CodeId;
                        if ((modCode == null) != (matchCode == null))
                        {
                            ExportObjects.Add(mObject);
                            return;
                        }
                        if (matchCode != null && modCode != null)
                        {
                            if (matchCode.Name.Content != modCode.Name.Content)
                            {
                                ExportObjects.Add(mObject);
                                return;
                            }
                        }
                    }
                }
                catch
                {
                    string msg = "";
                    msg += $"Event: {evType}, Subtype: {subType}";
                    ScriptMessage($"{mObject.Name.Content}\n{msg}\n Failed to be read!");
                }
            }
        }
    }));
}
async Task CompareCode()
{
    await Task.Run(() => Parallel.ForEach(Data.Code, new ParallelOptions { MaxDegreeOfParallelism = 8 }, code =>
    {
        IncrementProgressParallel();
        if (ignoreList.Code.Contains(code.Name.Content)) { return; }
        ThreadLocal<GlobalDecompileContext> DECOMPILE_CONTEXT = new ThreadLocal<GlobalDecompileContext>(() => new GlobalDecompileContext(Data, false));
        ThreadLocal<GlobalDecompileContext> vDECOMPILE_CONTEXT = new ThreadLocal<GlobalDecompileContext>(() => new GlobalDecompileContext(vData, false));
        UndertaleCode mCode = code;
        UndertaleCode CodeMatch = null;
        CodeMatch = vData.Code.ByName(mCode.Name.Content);
        
        if (CodeMatch is null)
        {
            if (mCode.ParentEntry is not null)
            {
                ExportFunctions.Add(mCode);
                return;
            }
            ExportCode.Add(mCode);
            return;
        }
        else
        {
            if (mCode.ParentEntry is not null) { return;} //dont want these functions, they arent new
            if (Data.KnownSubFunctions is null)
            {
                Decompiler.BuildSubFunctionCache(Data);
            }
            if (vData.KnownSubFunctions is null)
            {
                Decompiler.BuildSubFunctionCache(vData);
            }
            GenerateGMLCache(DECOMPILE_CONTEXT);
            GenerateGMLCache(vDECOMPILE_CONTEXT);
            try
            {
                string mDecompiledCode = Decompiler.Decompile(mCode, DECOMPILE_CONTEXT.Value);
                string vDecompiledCode = Decompiler.Decompile(CodeMatch, vDECOMPILE_CONTEXT.Value);
                if (mDecompiledCode != vDecompiledCode)
                {
                    ExportCode.Add(mCode);
                    return;
                }
            }
            catch
            {
                try
                {
                    string mDisassembled = mCode.Disassemble(Data.Variables, Data.CodeLocals.For(mCode));
                    string vDisassembled = CodeMatch.Disassemble(vData.Variables, vData.CodeLocals.For(CodeMatch));
                    if (mDisassembled != vDisassembled)
                    {
                        ExportCode.Add(mCode);
                        return;
                    }
                }
                catch
                {
                    ScriptMessage($"Unable to export GML or Assembly Instructions for Code {mCode.Name.Content}");
                }
            }
        }
    }));
}
async Task CompareScripts()
{
    await Task.Run(() => Parallel.ForEach(Data.Scripts, new ParallelOptions { MaxDegreeOfParallelism = 8 }, script =>
    {
        if (ignoreList.Scripts.Contains(script.Name.Content)) { return; }
        UndertaleScript mScript = script as UndertaleScript;
        UndertaleScript scriptMatch;
        scriptMatch = vData.Scripts.ByName(mScript.Name.Content);
        if (mScript.Code.ParentEntry is not null)
        {
            return;
        }
        if (scriptMatch is null || scriptMatch.Code.Name.Content != mScript.Code.Name.Content)
        {
            ExportScript.Add(mScript);
            return;
        }
    }));
}
async Task CompareRooms()
{
    await Task.Run(() => Parallel.ForEach(Data.Rooms, new ParallelOptions { MaxDegreeOfParallelism = 4 }, room =>
    {
        IncrementProgressParallel();
        if (ignoreList.Rooms.Contains(room.Name.Content)) { return; }
        UndertaleRoom mRoom = room;
        UndertaleRoom roomMatch;
        roomMatch = vData.Rooms.ByName(mRoom.Name.Content);
        if (roomMatch is null || roomMatch.Layers.Count != mRoom.Layers.Count || roomMatch.Width != mRoom.Width || roomMatch.Height != mRoom.Height)
        {
            ExportRooms.Add(mRoom);
            return;
        }
        if ((roomMatch?.CreationCodeId is not null && mRoom?.CreationCodeId is null) || (roomMatch?.CreationCodeId is null && mRoom?.CreationCodeId is not null))
        {
            ExportRooms.Add(mRoom);
            return;
        }
        if (roomMatch?.CreationCodeId is not null && mRoom?.CreationCodeId is not null)
        {
            if (roomMatch?.CreationCodeId.Name.Content != mRoom?.CreationCodeId.Name.Content)
            {
                ExportRooms.Add(mRoom);
                return;
            }
        }
        foreach (UndertaleRoom.GameObject mobj in mRoom.GameObjects)
        {
            bool objExists = false;
            foreach (UndertaleRoom.GameObject vobj in roomMatch.GameObjects)
            {
                if (vobj.InstanceID == mobj.InstanceID && vobj.ObjectDefinition.Name.Content == mobj.ObjectDefinition.Name.Content)
                {
                    if (vobj.ScaleX != mobj.ScaleX) { break; }
                    if (vobj.ScaleY != mobj.ScaleY) { break; }
                    if (vobj.Rotation != mobj.Rotation) { break; }
                    if (vobj.X != mobj.X) { break; }
                    if (vobj.Y != mobj.Y) { break; }
                    if ((vobj.CreationCode is null && mobj.CreationCode is not null) || (vobj.CreationCode is not null && mobj.CreationCode is null)) { break; }
                    else if (vobj.CreationCode is not null && mobj.CreationCode is not null)
                        if (vobj?.CreationCode.Name.Content != mobj?.CreationCode.Name.Content) { break; }
                    if ((vobj.PreCreateCode is null && mobj.PreCreateCode is not null) || (vobj.PreCreateCode is not null && mobj.PreCreateCode is null)) { break; }
                    else if (vobj.PreCreateCode is not null && mobj.PreCreateCode is not null)
                        if (vobj?.PreCreateCode.Name.Content != mobj?.PreCreateCode.Name.Content) { break; }
                    objExists = true;
                }
            }
            if (objExists == false)
            {
                ExportRooms.Add(mRoom);
                return;
            }
        }
        foreach (UndertaleRoom.Layer lay in mRoom.Layers)
        {
            UndertaleRoom.Layer vlay = null;
            foreach (UndertaleRoom.Layer laycandidate in roomMatch.Layers)
            {
                if (laycandidate.LayerName.Content == lay.LayerName.Content && laycandidate.LayerId == lay.LayerId)
                {
                    vlay = laycandidate;
                    break;
                }
            }
            if (vlay == null || lay.LayerDepth != vlay.LayerDepth || lay.XOffset != vlay.XOffset || lay.YOffset != vlay.YOffset || lay.IsVisible != vlay.IsVisible || lay.HSpeed != vlay.HSpeed || lay.VSpeed != vlay.VSpeed)
            {
                ExportRooms.Add(mRoom);
                return;
            }
            switch (lay.LayerType) 
            {
                case UndertaleRoom.LayerType.Background:
                    if ((vlay.BackgroundData.Sprite is not null && lay.BackgroundData.Sprite is null) || (vlay.BackgroundData.Sprite is null && lay.BackgroundData.Sprite is not null)) 
                    {
                        ExportRooms.Add(mRoom);
                        return;
                    }
                    else if (vlay.BackgroundData.Sprite is not null && lay.BackgroundData.Sprite is not null)
                    {
                        if (vlay.BackgroundData.Sprite.Name.Content != lay.BackgroundData.Sprite.Name.Content)
                        {
                            ExportRooms.Add(mRoom);
                            return;
                        }
                    }
                    if (vlay.BackgroundData.TiledHorizontally != lay.BackgroundData.TiledHorizontally || vlay.BackgroundData.TiledVertically != lay.BackgroundData.TiledVertically)
                    {
                        ExportRooms.Add(mRoom);
                        return;
                    }
                    break;
                case UndertaleRoom.LayerType.Tiles:
                    if ((lay.TilesData is null && vlay.TilesData is not null) || (lay.TilesData is not null && vlay.TilesData is null))
                    {
                        ExportRooms.Add(mRoom);
                        return;
                    }
                    if (lay.TilesData is not null && vlay.TilesData is not null)
                    {
                        if (lay?.TilesData.TilesX != vlay?.TilesData.TilesX || lay?.TilesData.TilesY != vlay?.TilesData.TilesY)
                        {
                            ExportRooms.Add(mRoom);
                            return;
                        }
                        if ((lay.TilesData.Background is not null && vlay.TilesData is null) || (lay.TilesData.Background is null && vlay.TilesData is not null))
                        {
                            ExportRooms.Add(mRoom);
                            return;
                        }
                        if (lay.TilesData.Background is not null && vlay.TilesData is not null)
                        {
                            if (lay.TilesData.Background.Name.Content != vlay.TilesData.Background.Name.Content)
                            {
                                ExportRooms.Add(mRoom);
                                return;
                            }
                        }
                        if ((lay.TilesData.TileData is not null && vlay.TilesData.TileData is null) || (lay.TilesData.TileData is null && vlay.TilesData.TileData is not null))
                        {
                            ExportRooms.Add(mRoom);
                            return;
                        }
                        if (lay.TilesData.TileData is not null && vlay.TilesData.TileData is not null)
                        {
                            StringBuilder sb = new();
                            foreach (uint[] dataRow in lay.TilesData.TileData)
                                sb.AppendLine(String.Join(";", dataRow.Select(x => x.ToString())));
                            string tiles = sb.ToString(); sb.Clear();
                            foreach (uint[] vdataRow in vlay.TilesData.TileData)
                                sb.AppendLine(String.Join(";", vdataRow.Select(x => x.ToString())));
                            string vtiles = sb.ToString();
                            if (tiles != vtiles)
                            {
                                ExportRooms.Add(mRoom);
                                return;
                            }
                        }
                    }
                    break;
                case UndertaleRoom.LayerType.Assets:
                    if (lay.AssetsData.Sprites.Count != vlay.AssetsData.Sprites.Count) //Only checking if there are more or less sprites, it might miss out on some being differently placed, but hopefully other checks grab that.
                    {
                        ExportRooms.Add(mRoom);
                        return;
                    }
                    break;
            }
        }
    }));
}
#endregion

#region Import
void ImportSprites(string dir, bool importAsSprite)
{
    //directly taken from ImportGraphics script
    string packDir = Path.Combine(ExePath, "Packager");
    Directory.CreateDirectory(packDir);
    Regex sprFrameRegex = new(@"^(.+?)(?:_(-*\d+))*$", RegexOptions.Compiled);
    string sourcePath = PrepareSprites(dir, importAsSprite, sprFrameRegex);
    string searchPattern = "*.png";
    string outName = Path.Combine(packDir, "atlas.txt");
    int textureSize = 16384; //I like em big
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
                UndertaleTexturePageItem texturePageItem = new UndertaleTexturePageItem();
                texturePageItem.Name = new UndertaleString("PageItem " + ++lastTextPageItem);
                texturePageItem.SourceX = (ushort)n.Bounds.X;
                texturePageItem.SourceY = (ushort)n.Bounds.Y;
                texturePageItem.SourceWidth = (ushort)n.Bounds.Width;
                texturePageItem.SourceHeight = (ushort)n.Bounds.Height;
                texturePageItem.TargetX = 0;
                texturePageItem.TargetY = 0;
                texturePageItem.TargetWidth = (ushort)n.Bounds.Width;
                texturePageItem.TargetHeight = (ushort)n.Bounds.Height;
                texturePageItem.BoundingWidth = (ushort)n.Bounds.Width;
                texturePageItem.BoundingHeight = (ushort)n.Bounds.Height;
                texturePageItem.TexturePage = texture;

                // Add this texture to UMT
                Data.TexturePageItems.Add(texturePageItem);

                // String processing
                string stripped = Path.GetFileNameWithoutExtension(n.Texture.Source);

                SpriteType spriteType = GetSpriteType(n.Texture.Source);

                if (importAsSprite)
                {
                    if ((spriteType == SpriteType.Unknown) || (spriteType == SpriteType.Font))
                    {
                        spriteType = SpriteType.Sprite;
                    }
                }

                setTextureTargetBounds(texturePageItem, stripped, n);


                if (spriteType == SpriteType.Tileset)
                {
                    UndertaleBackground background = Data.Backgrounds.ByName(stripped);
                    if (background != null)
                    {
                        background.Texture = texturePageItem;
                        background.Transparent = false;
                        background.Preload = false;
                        background.Texture = texturePageItem;
                        background.GMS2UnknownAlways2 = 2;
                        background.GMS2TileWidth = 32;
                        background.GMS2TileHeight = 32;
                        background.GMS2OutputBorderX = 0;
                        background.GMS2OutputBorderY = 0;
                        background.GMS2TileColumns = 1;
                        background.GMS2ItemsPerTileCount = 1;
                        background.GMS2TileCount = 1;
                        background.GMS2UnknownAlwaysZero = 0;
                        background.GMS2FrameLength = 66666;
                        //create tile id list
                        background.GMS2TileIds = new List<UndertaleBackground.TileID>();
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
                        newTileset.GMS2TileWidth = 32;
                        newTileset.GMS2TileHeight = 32;
                        newTileset.GMS2OutputBorderX = 0;
                        newTileset.GMS2OutputBorderY = 0;
                        newTileset.GMS2TileColumns = 1;
                        newTileset.GMS2ItemsPerTileCount = 1;
                        newTileset.GMS2TileCount = 1;
                        newTileset.GMS2UnknownAlwaysZero = 0;
                        newTileset.GMS2FrameLength = 66666;
                        Data.Backgrounds.Add(newTileset);
                        //create tile id list
                        newTileset.GMS2TileIds = new List<UndertaleBackground.TileID>();
                    }
                }
                else if (spriteType == SpriteType.Sprite)
                {
                    // Get sprite to add this texture to
                    string spriteName;
                    int frame = 0;
                    try
                    {
                        var spriteParts = sprFrameRegex.Match(stripped);
                        spriteName = spriteParts.Groups[1].Value;
                        Int32.TryParse(spriteParts.Groups[2].Value, out frame);
                    }
                    catch (Exception e)
                    {
                        ScriptMessage("Error: Image " + stripped + " has an invalid name. Skipping...");
                        continue;
                    }
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
                        newSprite.Width = (uint)n.Bounds.Width;
                        newSprite.Height = (uint)n.Bounds.Height;
                        newSprite.MarginLeft = 0;
                        newSprite.MarginRight = n.Bounds.Width - 1;
                        newSprite.MarginTop = 0;
                        newSprite.MarginBottom = n.Bounds.Height - 1;
                        newSprite.OriginX = 0;
                        newSprite.OriginY = 0;
                        if (frame > 0)
                        {
                            for (int i = 0; i < frame; i++)
                                newSprite.Textures.Add(null);
                        }
                        bool hasCollisonMask = makeCollisionBox(n.Texture.Source);
                        if (hasCollisonMask == true)
                        {
                            newSprite.CollisionMasks.Add(newSprite.NewMaskEntry());
                            Rectangle bmpRect = new Rectangle(n.Bounds.X, n.Bounds.Y, n.Bounds.Width, n.Bounds.Height);
                            System.Drawing.Imaging.PixelFormat format = atlasBitmap.PixelFormat;
                            Bitmap cloneBitmap = atlasBitmap.Clone(bmpRect, format);
                            int width = ((n.Bounds.Width + 7) / 8) * 8;
                            BitArray maskingBitArray = new BitArray(width * n.Bounds.Height);
                            for (int y = 0; y < n.Bounds.Height; y++)
                            {
                                for (int x = 0; x < n.Bounds.Width; x++)
                                {
                                    Color pixelColor = cloneBitmap.GetPixel(x, y);
                                    maskingBitArray[y * width + x] = (pixelColor.A > 0);
                                }
                            }
                            BitArray tempBitArray = new BitArray(width * n.Bounds.Height);
                            for (int i = 0; i < maskingBitArray.Length; i += 8)
                            {
                                for (int j = 0; j < 8; j++)
                                {
                                    tempBitArray[j + i] = maskingBitArray[-(j - 7) + i];
                                }
                            }
                            int numBytes;
                            numBytes = maskingBitArray.Length / 8;
                            byte[] bytes = new byte[numBytes];
                            tempBitArray.CopyTo(bytes, 0);
                            for (int i = 0; i < bytes.Length; i++)
                                newSprite.CollisionMasks[0].Data[i] = bytes[i];
                        }
                        newSprite.Textures.Add(texentry);
                        Data.Sprites.Add(newSprite);
                        continue;
                    }
                    if (frame > sprite.Textures.Count - 1)
                    {
                        while (frame > sprite.Textures.Count - 1)
                        {
                            sprite.Textures.Add(texentry);
                        }
                        continue;
                    }
                    sprite.Textures[frame] = texentry;
                }
            }
        }
        // Increment atlas
        atlasCount++;
    }
}

SpriteType GetSpriteType(string path)
{
    string folderPath = Path.GetDirectoryName(Path.GetDirectoryName(path));
    string folderName = new DirectoryInfo(folderPath).Name;
    string lowerName = folderName.ToLower();
    if (lowerName == "backgrounds" || lowerName == "background")
    {
        return SpriteType.Background;
    }
    else if (lowerName == "fonts" || lowerName == "font")
    {
        return SpriteType.Font;
    }
    else if (lowerName == "sprites" || lowerName == "sprite")
    {
        return SpriteType.Sprite;
    }
    else if (lowerName == "tilesets" || lowerName == "tileset")
    {
        return SpriteType.Tileset;
    }
    return SpriteType.Unknown;
}
string PrepareSprites(string dir, bool isSprite, Regex sprFrameRegex)
{
    string importFolder = dir;
    if (importFolder == null)
        throw new ScriptException("The import folder was not set.");

    //Stop the script if there's missing sprite entries or w/e.
    bool hadMessage = false;
    string currSpriteName = null;
    string[] dirFiles = Directory.GetFiles(importFolder, "*.png", SearchOption.AllDirectories);
    foreach (string file in dirFiles)
    {
        string FileNameWithExtension = Path.GetFileName(file);
        string stripped = Path.GetFileNameWithoutExtension(file);
        string spriteName = "";

        SpriteType spriteType = GetSpriteType(file);
        // Check for duplicate filenames
        string[] dupFiles = Directory.GetFiles(importFolder, FileNameWithExtension, SearchOption.AllDirectories);
        if (dupFiles.Length > 1)
            throw new ScriptException("Duplicate file detected. There are " + dupFiles.Length + " files named: " + FileNameWithExtension);

        // Sprites can have multiple frames! Do some sprite-specific checking.
        if (spriteType == SpriteType.Sprite)
        {
            var spriteParts = sprFrameRegex.Match(stripped);
            // Allow sprites without underscores
            if (!spriteParts.Groups[2].Success)
                continue;

            spriteName = spriteParts.Groups[1].Value;

            if (!Int32.TryParse(spriteParts.Groups[2].Value, out int frame))
                throw new ScriptException(spriteName + " has an invalid frame index.");
            if (frame < 0)
                throw new ScriptException(spriteName + " is using an invalid numbering scheme. The script has stopped for your own protection.");

            // If it's not a first frame of the sprite
            if (spriteName == currSpriteName)
                continue;

            string[][] spriteFrames = Directory.GetFiles(importFolder, $"{spriteName}_*.png", SearchOption.AllDirectories)
                                               .Select(x =>
                                               {
                                                   var match = sprFrameRegex.Match(Path.GetFileNameWithoutExtension(x));
                                                   if (match.Groups[2].Success)
                                                       return new string[] { match.Groups[1].Value, match.Groups[2].Value };
                                                   else
                                                       return null;
                                               })
                                               .OfType<string[]>().ToArray();
            if (spriteFrames.Length == 1)
            {
                currSpriteName = null;
                continue;
            }

            int[] frameIndexes = spriteFrames.Select(x =>
            {
                if (Int32.TryParse(x[1], out int frame))
                    return (int?)frame;
                else
                    return null;
            }).OfType<int?>().Cast<int>().OrderBy(x => x).ToArray();
            if (frameIndexes.Length == 1)
            {
                currSpriteName = null;
                continue;
            }

            for (int i = 0; i < frameIndexes.Length - 1; i++)
            {
                int num = frameIndexes[i];
                int nextNum = frameIndexes[i + 1];

                if (nextNum - num > 1)
                    throw new ScriptException(spriteName + " is missing one or more indexes.\nThe detected missing index is: " + (num + 1));
            }

            currSpriteName = spriteName;
        }
    }
    return importFolder;
}
#endregion

#region Form
public class IntroForm : Form
{
    #region Components
    private TabControl tabControl;
    private TabPage tabPage1;
    private TabPage tabPage2;
    private TabPage tabPage3;
    private CheckBox[] tab1CheckBoxes;
    private CheckBox[] tab2CheckBoxes;
    private CheckBox[] tab3CheckBoxes;
    private CheckBox pixelBox;
    private ComboBox ignoreTypeSelector;
    private Button continueExport;
    private Button continueImport;
    private Button findDataButton;
    private Button findProjectButton;
    private Button createIgnoreButton;
    private Button selectIgnoreButton;
    private TextBox projectFolderBox;
    private TextBox dataFileBox;
    private TextBox selectedIgnoreBox;
    private TextBox addIgnoreBox;
    private Button addIgnoreButton;
    private Button ImportProjectButton;
    private TextBox ImportProjectBox;
    private OpenFileDialog ignoreFileSelector;
    private FolderBrowserDialog createIgnoreDialog;
    private FolderBrowserDialog projectFolderSelector;
    private OpenFileDialog dataLocationSelector;
    #endregion
    #region Info
    private bool isFirstClick = true;
    public bool export { get; set; }
    public string projectFolder { get; set; }
    public string dataLocation { get; set; }
    public bool getSprites { get; set; }
    public bool getObjects { get; set; }
    public bool getCode { get; set; }
    public bool getRooms { get; set; }
    public bool comparePixels { get; set; }
    public bool[] useIgnoreList { get; set; }
    public string ignoreList { get; set; }
    private string B64Icon;
    #endregion
    public IntroForm()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        #region init
        this.Text = "Project Handler v1.3.0 by AwfulNasty";
        this.Size = new System.Drawing.Size(350, 200);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.MinimumSize = new Size(350, 200);
        this.MaximumSize = new Size(350, 200);
        tabControl = new TabControl();
        tabControl.Dock = DockStyle.Fill;
        tabPage1 = new TabPage("Export");
        tabPage2 = new TabPage("Import");
        tabPage3 = new TabPage("Ignore List");
        #endregion
        #region Export Tab
        tab1CheckBoxes = new CheckBox[4];
        for (int i = 0; i < 4; i++)
        {
            tab1CheckBoxes[i] = new CheckBox();
            string boxText = "";
            ToolTip toolTip = new ToolTip();
            switch (i)
            {
                case 3:
                    boxText = "Graphics";
                    toolTip.SetToolTip(tab1CheckBoxes[i], "Exports sprites and tilesets.");
                    tab1CheckBoxes[i].Click += IntroForm_Click;
                    break;
                case 1:
                    boxText = "Objects";
                    toolTip.SetToolTip(tab1CheckBoxes[i], "Exports objects and their properties.");
                    break;
                case 2:
                    boxText = "Code";
                    toolTip.SetToolTip(tab1CheckBoxes[i], "Exports code and scripts.");
                    break;
                case 0:
                    boxText = "Rooms";
                    toolTip.SetToolTip(tab1CheckBoxes[i], "Exports rooms.");
                    break;
            }
            tab1CheckBoxes[i].Text = boxText;
            tab1CheckBoxes[i].Location = new System.Drawing.Point(20, 25 + 22 * i);
            tabPage1.Controls.Add(tab1CheckBoxes[i]);
        }

        pixelBox = new CheckBox()
        {
            Text = "Pixel Compare",
            Location = new System.Drawing.Point(128, 91),
            Visible = false
        };
        ToolTip pixelBoxTip = new ToolTip();
        pixelBoxTip.SetToolTip(pixelBox, "Compare TexturePageItems pixel by pixel");
        pixelBox.Click += pixelWarning_Click;
        tabPage1.Controls.Add(pixelBox);

        continueExport = new Button()
        {
            Visible = true,
            Size = new System.Drawing.Size(75, 25),
            Location = new System.Drawing.Point(245, 100),
            Text = "Export"
        };
        continueExport.Click += ExportButton_Click;
        tabPage1.Controls.Add(continueExport);

        projectFolderSelector = new FolderBrowserDialog()
        {
            Description = "Select Project Folder",
            ShowNewFolderButton = true
        };

        findProjectButton = new Button()
        {
            Text = "…",
            Location = new System.Drawing.Point(301, 20),
            Size = new System.Drawing.Size(23, 23),
            Visible = true
        };
        findProjectButton.Click += ChooseFolderButton_Click;
        tabPage1.Controls.Add(findProjectButton);

        projectFolderBox = new TextBox()
        {
            Location = new System.Drawing.Point(150, 20),
            Size = new System.Drawing.Size(150, 25),
            Visible = true,
            Text = "Select Export Path"
        };

        tabPage1.Controls.Add(projectFolderBox);

        dataLocationSelector = new OpenFileDialog()
        {
            Title = "Select Data File",
            Filter = "Data Files (*.win)|*.win|All Files (*.*)|*.*"

        };

        findDataButton = new Button()
        {
            Text = "…",
            Location = new System.Drawing.Point(301, 50),
            Size = new System.Drawing.Size(23, 23),
            Visible = true
        };
        findDataButton.Click += ChooseDataButton_Click;
        tabPage1.Controls.Add(findDataButton);

        dataFileBox = new TextBox()
        {
            Location = new System.Drawing.Point(150, 50),
            Size = new System.Drawing.Size(150, 25),
            Visible = true,
            Text = "Select Vanilla Data"
        };
        tabPage1.Controls.Add(dataFileBox);
        #endregion
        #region Import Tab
        tab2CheckBoxes = new CheckBox[4];
        for (int i = 0; i < 4; i++)
        {
            ToolTip toolTip = new ToolTip();
            tab2CheckBoxes[i] = new CheckBox();
            string boxText = "";
            switch (i)
            {
                case 3:
                    boxText = "Graphics";
                    toolTip.SetToolTip(tab2CheckBoxes[i], "Imports sprites and tilesets.");
                    break;
                case 1:
                    boxText = "Objects";
                    toolTip.SetToolTip(tab2CheckBoxes[i], "Imports objects and their properties.");
                    break;
                case 2:
                    boxText = "Code";
                    toolTip.SetToolTip(tab2CheckBoxes[i], "Imports scripts and code.");
                    break;
                case 0:
                    boxText = "Rooms";
                    toolTip.SetToolTip(tab2CheckBoxes[i], "Imports rooms.");
                    break;
            }
            tab2CheckBoxes[i].Text = boxText;
            tab2CheckBoxes[i].Location = new System.Drawing.Point(20, 25 + 22 * i);
            tabPage2.Controls.Add(tab2CheckBoxes[i]);
        }

        

        continueImport = new Button()
        {
            Visible = true,
            Size = new System.Drawing.Size(75, 25),
            Location = new System.Drawing.Point(245, 100),
            Text = "Import"
        };
        continueImport.Click += ImportButton_Click;
        tabPage2.Controls.Add(continueImport);

        ImportProjectButton = new Button()
        {
            Text = "…",
            Location = new System.Drawing.Point(301, 20),
            Size = new System.Drawing.Size(23, 23),
            Visible = true
        };
        ImportProjectButton.Click += ImportProjectButton_Click;
        tabPage2.Controls.Add(ImportProjectButton);

        ImportProjectBox = new TextBox()
        {
            Location = new System.Drawing.Point(150, 20),
            Size = new System.Drawing.Size(150, 25),
            Visible = true,
            Text = "Select Project Path"
        };

        tabPage2.Controls.Add(ImportProjectBox);
        #endregion
        #region Ignore Tab
        #region Create Button
        createIgnoreButton = new Button()
        {
            Visible = true,
            Size = new System.Drawing.Size(90, 25),
            Location = new System.Drawing.Point(169, 10),
            Text = "Create New"
        };
        createIgnoreButton.Click += CreateIgnoreButton_Click;
        tabPage3.Controls.Add(createIgnoreButton);
        #endregion
        #region Display Box
        selectedIgnoreBox = new TextBox()
        {
            AllowDrop = true,
            Size = new System.Drawing.Size(130, 25),
            Location = new System.Drawing.Point(10, 11),
            Text = "Select IgnoreList.json"
        };
        selectIgnoreButton = new Button()
        {
            Text = "…",
            Location = new System.Drawing.Point(141, 11),
            Size = new System.Drawing.Size(23, 23),
            Visible = true
        };
        selectIgnoreButton.Click += SelectIgnoreButton_Click;
        tabPage3.Controls.Add(selectedIgnoreBox); tabPage3.Controls.Add(selectIgnoreButton);
        #endregion
        #region Add Ignore
        ignoreTypeSelector = new ComboBox()
        {
            Size = new System.Drawing.Size(100, 25),
            Location = new System.Drawing.Point(145, 40),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        string[] listTypes = { "Sprites", "Tilesets", "Code", "Scripts", "Objects", "Rooms"};
        for (int i = 0; i < listTypes.Length; i++)
        {
            ignoreTypeSelector.Items.Add(listTypes[i]);
        }
        ignoreTypeSelector.SelectedIndex = 0;
        tabPage3.Controls.Add(ignoreTypeSelector);
        addIgnoreBox = new TextBox()
        {
            Text = "Name of Asset to Add",
            Location = new System.Drawing.Point(10, 40),
            Size = new System.Drawing.Size(130, 25)
        };
        tabPage3.Controls.Add(addIgnoreBox);
        addIgnoreButton = new Button()
        {
            Text = "Add",
            Size = new System.Drawing.Size(50, 23),
            Location = new System.Drawing.Point(250, 39)
        };
        addIgnoreButton.Click += AddIgnoreButton_Click;
        tabPage3.Controls.Add(addIgnoreButton);
        #endregion
        #region Use Checkboxes
        tab3CheckBoxes = new CheckBox[2];
        for (int i = 0; i < 2; i++)
        {
            tab3CheckBoxes[i] = new CheckBox();
            string boxText = "";
            ToolTip toolTip = new ToolTip();
            switch (i)
            {
                case 0:
                    boxText = "Use in Exports";
                    toolTip.SetToolTip(tab3CheckBoxes[i], "Apply the Ignore List for Exports");
                    break;
                case 1:
                    boxText = "Use in Imports";
                    toolTip.SetToolTip(tab3CheckBoxes[i], "Apply the Ignore List for Imports");
                    break;
            }
            tab3CheckBoxes[i].Text = boxText;
            tab3CheckBoxes[i].Location = new System.Drawing.Point(10 + 130 * i, 70);
            tabPage3.Controls.Add(tab3CheckBoxes[i]);
        }
        #endregion
        #endregion
        tabControl.TabPages.Add(tabPage1);
        tabControl.TabPages.Add(tabPage2);
        tabControl.TabPages.Add(tabPage3);
        this.Controls.Add(tabControl);

        tabPage1.Paint += Description_Paint;
        tabPage2.Paint += TabPage2_Paint;
        #region icon
        B64Icon = "AAABAAEAQEAAAAEAIAAoQgAAFgAAACgAAABAAAAAgAAAAAEAIAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/wAAAP8AAAD/AAAA/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP8AAAD/AAAA/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/AAAA/wAAAP8AAAD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/wAAAP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAP8AAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg////////////wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/AAAA/wAAAP8AAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/////////////////8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD//////8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD//////////////////////8CwoP/AsKD/wLCg/wAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAAAAAAAAAAAAAAAAAD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/AAAA/wAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAA/8CwoP/AsKD/wLCg///////AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAD/AAAA/8CwoP/AsKD//////////////////////8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP8AAAD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AsKD/wLCg///////////////////////AsKD/wLCg/8CwoP/AsKD/wLCg/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/wAAAP8AAAD/AAAA/wAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/8CwoP/AsKD/wLCg/8CwoP/AsKD/////////////////wLCg/8CwoP8AAAD/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/AAAA/wAAAP8AAAD/wLCg/8CwoP/AsKD/wLCg/8CwoP/AsKD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA///////////////////////////////////8H/w//////4AP+Af/////AAfwAf////4AB+AA/////AAH4AD////8AAAAAf////wAAAAD/////AAAAAf////+AAAAD+f///4AAAAPg////wAAAAYB////AAAAAAD///4AAAAAAH///AAAAAAAf//4AAAAAAA//gAAAAAAAD/8AAAAAAAAP/gAAAAAAAA/8AAAAAAAAD/wAAAAAAAAf/AAAB/gAAD/8AAAP/AAAf/wAAB//AAH//AAAP/+AA//8AAA//8AD//4AAH//wAH//wAAf//gAf//gAD//+AB///AAP//4AAP//gA///gAAf/+AD//+AAB//wAP//4AAH//AAf//AAAf/4AA//4AAB//AAB//gAAH/4AAD/8AAA//AAAD/gAAH/4AAAAAAAAf/gAAAAAAD//8AAAAAAAP//wAAAAAAA///gAAAAAAD//+AAAAAAAH//4AAAAAAAP//wAAAAAAAf//AAAAAAAB//8AAAAAAAH//4AAAAAAAP//wHAAAAAA/////AAAAAH////+AAYAA/////4AB4AP/////gAHwD/////+AA/g//////8AD////////4AP////////+B///////////////////////////////////////////////8=";
        byte[] iconBytes = Convert.FromBase64String(B64Icon);
        using (MemoryStream stream = new MemoryStream(iconBytes))
        {
            Icon icon = new Icon(stream);
            this.Icon = icon;
        }
        #endregion
        this.ShowDialog();
    }

    #region Functions
    private void TabPage2_Paint(object sender, PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        Font font = new Font("Arial", 9);
        SolidBrush brush = new SolidBrush(Color.Black);
        Point textLocation = new System.Drawing.Point(5, 5);
        string text = "Data to Import:";
        graphics.DrawString(text, font, brush, textLocation);
        Point text2Location = new System.Drawing.Point(150, 45);
        string text2 = $"When importing code, ensure\nthat you compare and merge\nchanges between updates.";
        graphics.DrawString(text2, font, brush, text2Location);
        font.Dispose();
        brush.Dispose();
        graphics.Dispose();
    }
    private void IntroForm_Click(object sender, EventArgs e)
    {
        pixelBox.Visible ^= true;
    }
    private void Description_Paint(object sender, PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        Font font = new Font("Arial", 9);
        SolidBrush brush = new SolidBrush(Color.Black);
        Point textLocation = new System.Drawing.Point(5, 5);
        string text = "Data to Compare:";
        graphics.DrawString(text, font, brush, textLocation);
        font.Dispose();
        brush.Dispose();
        graphics.Dispose();
    }
    private void ChooseFolderButton_Click(object sender, EventArgs e)
    {
        DialogResult result = projectFolderSelector.ShowDialog();
        if (result == DialogResult.OK)
        {
            string selectedFolder = projectFolderSelector.SelectedPath;
            projectFolderBox.Text = selectedFolder;
        }
    }
    private void pixelWarning_Click(object sender, EventArgs e)
    {
        if (isFirstClick)
        {
            MessageBox.Show($"This option compares textures pixel by pixel!\nIt is extremely resource intensive and will take a long time.\n\nOnly select if you have manually replaced texturepageitems instead of using import scripts.", $"WARNING");
            isFirstClick = false;
        }
    }
    private void ImportProjectButton_Click(object sender, EventArgs e)
    {
        DialogResult result = projectFolderSelector.ShowDialog();
        if (result == DialogResult.OK)
        {
            string selectedFolder = projectFolderSelector.SelectedPath;
            ImportProjectBox.Text = selectedFolder;

        }
    }
    private void ChooseDataButton_Click(object sender, EventArgs e)
    {
        DialogResult result = dataLocationSelector.ShowDialog();
        if (result == DialogResult.OK)
        {
            string selectedData = dataLocationSelector.FileName;
            dataFileBox.Text = selectedData;
        }
    }
    private void ExportButton_Click(object sender, EventArgs e)
    {
        getSprites = tab1CheckBoxes[3].Checked;
        getObjects = tab1CheckBoxes[1].Checked;
        getCode = tab1CheckBoxes[2].Checked;
        getRooms = tab1CheckBoxes[0].Checked;
        comparePixels = pixelBox.Checked;
        projectFolder = projectFolderBox.Text + @"\";
        dataLocation = dataFileBox.Text;
        export = true;
        if (tab3CheckBoxes[0].Checked && File.Exists(selectedIgnoreBox.Text))
        {
            ignoreList = File.ReadAllText(selectedIgnoreBox.Text);
        }
        if (File.Exists(dataLocation) && Directory.Exists(projectFolder))
            this.Close();
            else
            {
                MessageBox.Show("Unable to continue - Data file and Project Path need to be set!", "Error - Select Directories");
            }
    }
    private void ImportButton_Click(object sender, EventArgs e)
    {
        getSprites = tab2CheckBoxes[3].Checked;
        getObjects = tab2CheckBoxes[1].Checked;
        getCode = tab2CheckBoxes[2].Checked;
        getRooms = tab2CheckBoxes[0].Checked;
        comparePixels = pixelBox.Checked;
        projectFolder = ImportProjectBox.Text + @"\";
        export = false;
        if (tab3CheckBoxes[1].Checked && File.Exists(selectedIgnoreBox.Text))
        {
            ignoreList = File.ReadAllText(selectedIgnoreBox.Text);
        }
        if (Directory.Exists(projectFolder))
            this.Close();
        else
        {
            MessageBox.Show("Unable to continue - Project Path needs to be set!", "Error - Select Directories");
        }
    }
    private void AddIgnoreButton_Click(object sender, EventArgs e)
    {
        string jsonPath = selectedIgnoreBox.Text;
        int selectedType = ignoreTypeSelector.SelectedIndex;
        string assetName = addIgnoreBox.Text;
        if ((!File.Exists(jsonPath)) || (!Path.GetExtension(jsonPath).Equals(".json", StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("Selected JSON does not exist!", "ERROR"); return; }
        if (selectedType == -1) { MessageBox.Show("You need to select an asset type first!", "ERROR"); return; }
        if (assetName is null || assetName == "Name of Asset to Add") { MessageBox.Show("You need to input an asset name first!", "ERROR"); return; }
        string json = File.ReadAllText(jsonPath);
        IgnoreList iList = new IgnoreList();
        iList = JsonConvert.DeserializeObject<IgnoreList>(json);
        switch (selectedType)
        {
            case 0://sprites
                iList.Sprites.Add(assetName);
                break;
            case 1://tilesets
                iList.Tilesets.Add(assetName);
                break;
            case 2://code
                iList.Code.Add(assetName);
                break;
            case 3://scripts
                iList.Scripts.Add(assetName);
                break;
            case 4://objects
                iList.Objects.Add(assetName);
                break;
            case 5://rooms
                iList.Rooms.Add(assetName);
                break;
        }
        string newJSON = JsonConvert.SerializeObject(iList, Formatting.Indented);
        File.WriteAllText(jsonPath, newJSON);
        addIgnoreBox.Text = "Name of Asset to Add";
    }
    private void SelectIgnoreButton_Click(object sender, EventArgs e)
    {
        ignoreFileSelector = new OpenFileDialog()
        {
            Title = "Select Ignore List",
            Filter = "JSON Files|*.json"
        };
        DialogResult result = ignoreFileSelector.ShowDialog();
        if (result == DialogResult.OK)
        {
            string selectedJSON = ignoreFileSelector.FileName;
            selectedIgnoreBox.Text = selectedJSON;
        }
    }
    private void CreateIgnoreButton_Click(object sender, EventArgs e)
    {
        createIgnoreDialog = new FolderBrowserDialog()
        {
            ShowNewFolderButton = true,
            Description = "Folder to Create New Ignore List",
            UseDescriptionForTitle = true
        };
        DialogResult result = createIgnoreDialog.ShowDialog();
        if (result == DialogResult.OK)
        {
            string selectedFolder = createIgnoreDialog.SelectedPath;
            string jsonPath = Path.Combine(selectedFolder, "IgnoreList.json");
            IgnoreList jsonData = new IgnoreList
            {
                Code = new List<string>(),
                Objects = new List<string>(),
                Rooms = new List<string>(),
                Scripts = new List<string>(),
                Sprites = new List<string>(),
                Tilesets = new List<string>()
            };
            string json = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
            File.WriteAllText(jsonPath, json);
            MessageBox.Show("Ignore List JSON created successfully!");
            System.Diagnostics.Process.Start("explorer.exe", selectedFolder);
        }
    }
    #endregion
}
#endregion

#region texture packer
void setTextureTargetBounds(UndertaleTexturePageItem tex, string textureName, Node n)
{
    tex.TargetX = 0;
    tex.TargetY = 0;
    tex.TargetWidth = (ushort)n.Bounds.Width;
    tex.TargetHeight = (ushort)n.Bounds.Height;
}
public class TextureInfo
{
    public string Source;
    public int Width;
    public int Height;
}

public enum SpriteType
{
    Sprite,
    Tileset,
    Background,
    Font,
    Unknown
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
        List<TextureInfo> pretextures = new List<TextureInfo>();
        pretextures = SourceTextures.ToList();
        List<List<TextureInfo>> Textures = new List<List<TextureInfo>>();
        int batchsize = 50;
        for (int i = 0; i < pretextures.Count; i++)
        {
            Textures.Add(pretextures.Skip(i * batchsize).Take(batchsize).ToList());
        }
        //2: generate as many atlasses as needed (with the latest one as small as possible)
        Atlasses = new List<Atlas>();
        foreach (List<TextureInfo> tex in Textures)
        {
            List<TextureInfo> textures = tex;
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
        ResolutionFix.SetResolution(96.0F, 96.0F);
        Image img2 = ResolutionFix;
        img.Dispose();
        g.Dispose();
        return img2;
        // DPI FIX END
    }
}
#endregion