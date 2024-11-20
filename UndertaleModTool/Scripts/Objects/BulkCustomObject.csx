// By Creepersbane
// v1.0 - 08/15/2020

EnsureDataLoaded();

ScriptMessage("Simple method to bulk add custom objects to a GameMaker Studio 2 game, Script by __Boxed#0469 (boxed0469 on discord)");

string objname = SimpleTextInput("Object Name", "Object Name (one object per line)", "", true);

using (StringReader reader = new StringReader(objname))
{
    string line;
    while ((line = reader.ReadLine()) != null)
    {
		var new_obj = Data.GameObjects.ByName(line);

		if (new_obj == null)
		{
			new_obj = new UndertaleGameObject()
			{
				Name = Data.Strings.MakeString(line)
			};
			Data.GameObjects.Add(new_obj);
		}

		new_obj.EventHandlerFor(EventType.Create, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML(@"", Data);
		new_obj.EventHandlerFor(EventType.Draw, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML(@"", Data);
		new_obj.EventHandlerFor(EventType.Step, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML(@"", Data);
		new_obj.EventHandlerFor(EventType.Alarm, (uint)0, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML("", Data);
		new_obj.EventHandlerFor(EventType.Destroy, Data.Strings, Data.Code, Data.CodeLocals).ReplaceGML("", Data);
    }
}

ScriptMessage("how add checkmark");