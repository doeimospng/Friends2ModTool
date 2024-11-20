/// NekoPresence v1.2 for UndertaleModTool
/// UndertaleModTool friendly version of the NekoPresence extension, designed to be injected into games.
/// @author Nikita Krapivin
/// @version v1.2.0

void InjectNP()
{
	var extension = new UndertaleExtension()
	{
		ClassName = Data.Strings.MakeString(""),
		FolderName = Data.Strings.MakeString(""),
		Name = Data.Strings.MakeString("NekoPresence"),
		Files = new UndertalePointerList<UndertaleExtensionFile>()
	};
	
	extension.Files.Add(
		new UndertaleExtensionFile()
		{
			Filename = Data.Strings.MakeString("NekoPresence.dll"),
			InitScript = Data.Strings.MakeString("__np_initdll"),
			CleanupScript = Data.Strings.MakeString("__np_shutdown"),
			Kind = UndertaleExtensionKind.Dll,
			Functions = new UndertalePointerList<UndertaleExtensionFunction>()
			{
				new UndertaleExtensionFunction()
				{
					ID = 1,
					ExtName = Data.Strings.MakeString("np_initdll"),
					Kind = 11,
					Name = Data.Strings.MakeString("__np_initdll"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>(),
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 2,
					ExtName = Data.Strings.MakeString("np_shutdown"),
					Kind = 11,
					Name = Data.Strings.MakeString("__np_shutdown"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>(),
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 3,
					ExtName = Data.Strings.MakeString("np_initdiscord"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_initdiscord"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 4,
					ExtName = Data.Strings.MakeString("np_setpresence"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_setpresence"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 5,
					ExtName = Data.Strings.MakeString("np_update"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_update"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>(),
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 6,
					ExtName = Data.Strings.MakeString("RegisterCallbacks"),
					Kind = 11,
					Name = Data.Strings.MakeString("__np_registercallbacks_do_not_call"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 7,
					ExtName = Data.Strings.MakeString("np_setpresence_more"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_setpresence_more"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 8,
					ExtName = Data.Strings.MakeString("np_clearpresence"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_clearpresence"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>(),
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 9,
					ExtName = Data.Strings.MakeString("np_registergame"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_registergame"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 10,
					ExtName = Data.Strings.MakeString("np_registergame_steam"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_registergame_steam"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 11,
					ExtName = Data.Strings.MakeString("np_respond"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_respond"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 12,
					ExtName = Data.Strings.MakeString("np_setpresence_secrets"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_setpresence_secrets"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 13,
					ExtName = Data.Strings.MakeString("np_setpresence_partyparams"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_setpresence_partyparams"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.String },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
					},
					RetType = UndertaleExtensionVarType.Double
				},
				new UndertaleExtensionFunction()
				{
					ID = 14,
					ExtName = Data.Strings.MakeString("np_setpresence_timestamps"),
					Kind = 11,
					Name = Data.Strings.MakeString("np_setpresence_timestamps"),
					Arguments = new UndertaleSimpleList<UndertaleExtensionFunctionArg>()
					{
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
						new UndertaleExtensionFunctionArg() { Type = UndertaleExtensionVarType.Double },
					},
					RetType = UndertaleExtensionVarType.Double
				},
			}
		});
		
	extension.Files.Add(
			new UndertaleExtensionFile()
			{
				Filename = Data.Strings.MakeString("NekoPresence.gml"),
				InitScript = Data.Strings.MakeString(""),
				CleanupScript = Data.Strings.MakeString(""),
				Kind = UndertaleExtensionKind.GML,
				Functions = new UndertalePointerList<UndertaleExtensionFunction>()
			}
		);
		
	Data.Extensions.Add(extension);

	// generate "function" entries for every extension function EXCEPT THE HIDDEN ONES!
	foreach (var function in extension.Files[0].Functions)
	{
		if (function.Name.Content == "__np_initdll" || function.Name.Content == "__np_shutdown") continue;
		Data.Functions.Add(new UndertaleFunction()
		{
			Name = function.Name
		});
	}
	
	// add productId
	if (Data.IsGameMaker2() || Data.GeneralInfo.Build == 9999)
	{
		byte[] throwawayData = System.Text.Encoding.ASCII.GetBytes("NEKOPRESENCEUTMT");
		Data.FORM.EXTN.productIdData.Add(throwawayData);
	}
}

// We start here.
if (!ScriptQuestion("This will modify your .win, be sure to make a backup, proceed?")) return;
InjectNP();
ScriptMessage("Done! Save and load the .win & start rocking!");
