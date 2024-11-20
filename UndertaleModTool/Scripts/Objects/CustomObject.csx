// By Creepersbane
// v1.0 - 08/15/2020

EnsureDataLoaded();

ScriptMessage("Simple method to add a custom object to a GameMaker Studio 2 game, Script by __Boxed#0469 (boxed0469 on discord)");

string objname = SimpleTextInput("Object Name", "Object Name (obj_example)", "", false);

// Blaster
var new_obj = Data.GameObjects.ByName(objname);

if (new_obj == null)
{
    new_obj = new UndertaleGameObject()
    {
        Name = Data.Strings.MakeString(objname)
    };
    Data.GameObjects.Add(new_obj);
}

new_obj.EventHandlerFor(EventType.Create, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML(@"", Data);

new_obj.EventHandlerFor(EventType.Draw, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML(@"
	draw_sprite_ext(sprite_index, image_index, x, y, image_xscale, image_yscale, image_angle, image_blend, image_alpha)
", Data);
new_obj.EventHandlerFor(EventType.Draw, (uint)64, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML(@"
", Data);

new_obj.EventHandlerFor(EventType.Step, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML(@"", Data);

new_obj.EventHandlerFor(EventType.Alarm, (uint)0, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML("", Data);

new_obj.EventHandlerFor(EventType.Destroy, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML("", Data);

ScriptMessage("how add checkmark");


