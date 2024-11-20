/*
    Exports sprites as a GIF, using an external GIF libary because .NET's built-in one sucks.

    Script made by CST1229, with parts based off of ExportAllSprites.csx.
    Uses the AnimatedGif library https://github.com/mrousavy/AnimatedGif
 */

using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UndertaleModLib.Util;

EnsureDataLoaded();

string scriptFolder = Path.GetDirectoryName(ScriptPath);
string packagePath = Path.Join(scriptFolder, "AnimatedGif.dll");
if (!File.Exists(packagePath)) {
    ScriptError("AnimatedGif.dll not found! Please place it next to this script in its folder.");
    return;
}

// here's where the magic happens!
// we load the .dll using Assembly.LoadFrom, then use GetType to get properties
// then we have to do weird stuff to actually call functions/get properties from there
// because it doesn't return the actual thing and instead just a type
Assembly animGif = Assembly.LoadFrom(packagePath);
Type AnimatedGif = animGif.GetType("AnimatedGif.AnimatedGif");
Type AnimatedGifCreator = animGif.GetType("AnimatedGif.AnimatedGifCreator");
Type GifQuality = animGif.GetType("AnimatedGif.GifQuality");
// get an enum value
var Bit8 = GifQuality.GetEnumValues().GetValue(Array.IndexOf(GifQuality.GetEnumNames(), "Bit8"));

ScriptMessage("Please select an output directory");
string folder = PromptChooseDirectory();
if (folder == null)
    return;
folder = Path.GetDirectoryName(folder);

string filter = SimpleTextInput("Filter sprites", "String that the sprite names must start with (or leave blank to export all):", "", false);
await ExtractSprites(folder, filter);
ScriptMessage($"Sprite GIFs exported to: {folder}");

private async Task ExtractSprites(string folder, string prefix)
{
    TextureWorker worker = new TextureWorker();
    try
    {
        IList<UndertaleSprite> sprites = Data.Sprites;
        if (prefix != "")
        {
            sprites = new List<UndertaleSprite> { };
            foreach (UndertaleSprite sprite in Data.Sprites)
            {
                if (sprite.Name.Content.StartsWith(prefix))
                {
                    sprites.Add(sprite);
                }
            }
        }

        SetProgressBar(null, "Exporting sprites to GIF...", 0, sprites.Count);
        StartProgressBarUpdater();
        await Task.Run(() => Parallel.ForEach(sprites, (sprite) => {
            IncrementProgressParallel();
            ExtractSprite(sprite, folder, worker);
        }));
        await StopProgressBarUpdater();
        HideProgressBar();
    }
    catch (Exception e)
    {
        throw;
    }
    finally
    {
        worker.Cleanup();
    }
}

private void ExtractSprite(UndertaleSprite sprite, string folder, TextureWorker worker)
{
    // call AnimatedGif.Create
    using var gif = (IDisposable)AnimatedGif.InvokeMember(
        "Create",
        BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static,
        null, null,
        new Object[] { Path.Join(folder, sprite.Name.Content + ".gif"), 33, 0 }
    );
    for (int picCount = 0; picCount < sprite.Textures.Count; picCount++)
    {
        if (sprite.Textures[picCount]?.Texture != null)
        {
            Bitmap image = worker.GetTextureFor(sprite.Textures[picCount].Texture, sprite.Name.Content + " (frame " + picCount + ")", true);
            // call AnimatedGifCreator.AddFrame
            AnimatedGifCreator.InvokeMember(
                "AddFrame",
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                null, gif,
                new Object[] { image, -1, Bit8 }
            ); 
        }
    }
}