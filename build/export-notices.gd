extends SceneTree

func _initialize() -> void:
	var output := FileAccess.open("res://../../artifacts/GODOT-NOTICES.txt", FileAccess.WRITE)
	if output == null:
		push_error("Cannot write engine notices")
		quit(1)
		return
	output.store_string("Godot " + Engine.get_version_info()["string"] + "\n\n" + Engine.get_license_text() + "\n\nCOMPONENT ATTRIBUTIONS\n")
	output.store_string(JSON.stringify(Engine.get_copyright_info(), "\t") + "\n\nLICENSE TEXTS\n")
	var licenses := Engine.get_license_info()
	var names := licenses.keys()
	names.sort()
	for name in names:
		output.store_string("\n" + name + "\n" + licenses[name] + "\n")
	output.close()
	print("ENGINE_NOTICES_OK")
	quit()
