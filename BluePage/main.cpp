#define _CRT_SECURE_NO_WARNINGS

// Converting long64 and double
#pragma warning(disable:4244)
// Unused tab
#pragma warning(disable:4102)

#include <iostream>
#include <stack>
#include <map>
#include <vector>
#include <string>
#include <cstdio>
#include <set>
#include <windows.h>
#include <ctime>
#include <cmath>
#include <clocale>
#include <thread>
#include <mutex>
#include "blue.h"
using namespace std;

string target_path = "", header = "", content = "";

// These are common macros, do not delete them:
#define dshell_check(req) do {if (spl.size() < req) {cout << "Bad command" << endl; goto dend;}} while (false)
#define postback_check(req) do {if (descmd.size() < req) {raise_global_ce("Error: required parameter not given in postback settings"); }} while (false)

char post_buf1[8192] = {};
string utf_target = "";
bool using_utf = false;	// Which means put content into another file

// Set it to 0 when ready
#define RAW_POST_TEST 0
#define ECHO_TEST 0

void generateGlobalClass(string variable, string classname, varmap &myenv) {
	myenv.set_global(variable, null);
	myenv.set_global(variable + ".__type__", classname);
}

int main(int argc, char* argv[]) {
	//setlocale(LC_ALL, "C");
	stdouth = GetStdHandle(STD_OUTPUT_HANDLE);

	interpreter nenv;

#define raise_global_ce(description) nenv.raiseError(null, nenv.myenv, "BluePage Interpreter", 0, -2, description)

	// Test: Input code here:
#pragma region Compiler Test Option
#if _DEBUG
	// Warning: When testing VBWeb can't use it
#if RAW_POST_TEST
	string code = "", file = "test3.bp";
	target_path = "test3.target.html";
	nenv.in_debug = true;
	nenv.no_lib = false;
#else
	string code = "", file = "";
	target_path = "";
	nenv.in_debug = false;
	nenv.no_lib = false;
#endif

	if (code.length()) {
		specialout();
		cout << code;
		cout << endl << "-------" << endl;
		endout();
	}
#else
	// DO NOT CHANGE
	string code = "", file = "";
	nenv.in_debug = false;
	nenv.no_lib = false;
#endif
	string version_info = string("BluePage Interpreter\nVersion 6.2\nIncludes:\n\nBlueBetter Interpreter\nVersion 1.25\nCompiled on ") + __DATE__ + " " + __TIME__ + "\nBluePage is an internal application which is used to support the access of .bp (BluePage file) and postback.";
#pragma endregion
	// End

	if (argc <= 1 && !file.length() && !code.length()) {
		cout << "Usage: " << argv[0] << " filename --target:[target] [options]";
		return 1;
	}
	
#pragma region Read Options

	if (!file.length() && !code.length()) {
		file = argv[1];
	}

	if (file == "--version") {
		// = true;
		cout << version_info << endl;
		return 0;
	}

	nenv.env_name = file;
	map<string, intValue> reqs = { {"FILE_NAME", intValue(file)}, {"__BLUEPAGE__", intValue(1)} };
	map<string, interpreter::bcaller> callers;	// Insert your requirements here

#if RAW_POST_TEST
	// For postback testers:
	reqs["IS_POSTBACK"] = intValue(1);
	reqs["SELF_POST"] = intValue("/test3.bp");
	reqs["page_mode"] = intValue(1);
	reqs["keeper.commander"] = intValue("command_test_output.txt");
	reqs["postback.data.document.utype.value"] = intValue("a*b*c");
	reqs["postback.data.field"] = intValue("test_onclick");
	utf_target = "utf_test_output.html";
	// End.
#endif

	for (int i = 2; i < argc; i++) {
		string opt = argv[i];
		if (opt == "--debug") {
			nenv.in_debug = true;
		}
		else if (beginWith(opt, "--const:")) {
			// String values only
			vector<string> spl = split(opt, ':', 1);
			if (spl.size() < 2) {
				curlout();
				cout << "Error: Bad format of --const option" << endl;
				endout();
				return 1;
			}
			vector<string> key_value = split(spl[1], '=', 1);
			if (key_value.size() < 2) {
				// For SELF_POST here's a special support:
				if (key_value[0] == "SELF_POST") {
					reqs["SELF_POST"] = intValue("");	// Means root directory
				}
				else {
					reqs[key_value[0]] = null;
				}
				
			}
			else {
				reqs[key_value[0]] = intValue(key_value[1]);
			}
			
			
		}
		else if (beginWith(opt, "--target:")) {
			vector<string> spl = split(opt, ':', 1);
			target_path = spl[1];
		}
		else if (beginWith(opt, "--include-from:")) {
			vector<string> spl = split(opt, ':', 1);
			if (spl.size() < 2) {
				curlout();
				cout << "Error: Bad format of --include-from option" << endl;
				endout();
				return 1;
			}
			nenv.include_sources.push_back(spl[1]);
		}
		else if (opt == "--no-lib") {
			//no_lib = true;
			curlout();
			cout << "Error: --no-lib is not for BluePage!" << endl;
			endout();
			return 2;
		}
	}
#pragma endregion

	if (target_path.length() <= 0) {
		curlout();
		cout << "Error: Target path is not given. use --target:Path." << endl;
		endout();
		return 1;
	}

	if (!code.length()) {
		FILE *f = fopen(file.c_str(), "r");
		if (f != NULL) {
			while (!feof(f)) {
				memset(buf1, 0, sizeof(char)*65536);	// Preventing repeating lines
				fgets(buf1, 65536, f);
				code += buf1;
			}
		}
	}

	if (nenv.in_debug) {
		begindout();
		cout << "Debug mode" << endl;
		string command = "";
		do {
			cout << "-> ";
			//cin >> command;
			getline(cin, command);
			vector<string> spl = split(command, ' ', 1);
			if (spl.size() <= 0) continue;
			if (spl[0] == "quit") {
				exit(0);
			}
		} while (command != "run");
		endout();
	}

	// Preprocess the code ...
	const string blue_start = "<?blue";
	const string blue_end = "?>";

	interpreter::bcaller initial_echo = interpreter::bcaller([](string exp, varmap &env, interpreter &interp) -> intValue {
		intValue output = interp.calculate(exp, env);
#if ECHO_TEST
		specialout();
		cout << "initially echo: \"" << output.str << "\"\n";
		endout();
#endif
		header += output.str;
		return null;
	});

	interpreter::bcaller normal_echo = interpreter::bcaller([](string exp, varmap &env, interpreter &interp) -> intValue {
		intValue output = interp.calculate(exp, env);
#if ECHO_TEST
		specialout();
		cout << "normally echo: \"" << output.str << "\"\n";
		endout();
#endif
		content += output.str;
		return null;
	});

	interpreter::bcaller utf_call = interpreter::bcaller([](string exp, varmap &env, interpreter &interp) -> intValue {
		if (!utf_target.length()) {
			interp.raiseError(null, env, "Internal processor of BluePage", 0, 0, "UTF Convert requirement is not supported");
			return null;
		}
		using_utf = true;
		return null;
	});

	// Helpful constants
	reqs["BBEGIN"] = intValue(blue_start);
	reqs["BEND"] = intValue(blue_end);

	// General environment
	varmap &keep_env = nenv.myenv;
	keep_env.push();	// We still require this ...?

	// Load libraries
	string codestream = "";

	auto lib_reader = [&codestream](const char* name) {
		FILE *fb = fopen(name, "r");
		if (fb != NULL) {
			while (!feof(fb)) {
				memset(buf1, 0, sizeof(buf1));
				fgets(buf1, 65536, fb);
				codestream += buf1;
				codestream.push_back('\n');
			}
			fclose(fb);
		}
		else {
			curlout();
			cout << "Bad standard library: " << name << endl;
			endout();
		}
		
	};

	// Initalize libraries right here
	//lib_reader("bmain.blue");	// There's no need
	lib_reader("document.blue");
	lib_reader("BluePage.blue");
	lib_reader("WebHeader.blue");

#if ECHO_TEST
	curlout();
	cout << codestream << endl;
	endout();
#endif

	// Put document as a new object ... (SHOULD BE GLOBAL ?!)
	//generateClass("document", "object", keep_env, false);
	generateGlobalClass("document", "object", keep_env);
	
	// Preprocessor (for all tags)
	const char html_begin = '<';
	const char html_end = '>';
	const string html_mark = "<!--";	// After beginner
	const string html_mark_end = "-->";
	const string id_matcher = " id=";
	const set<char> html_dispose = { '?', '!', '/' };
	size_t next_pos = 0, next_end_pos = 0;
	while ((next_pos = code.find(html_begin, next_pos)) != string::npos) {
		next_end_pos = code.find(html_end, next_pos);
		if (next_end_pos == string::npos) {
			raise_global_ce("HTML block is not closed");
		}
		else {

			if (code.find(blue_start, next_pos) == next_pos) {
				// Deal with usual BluePage mark
				// Go until blue_end.
				next_pos = code.find(blue_end, next_pos) + blue_end.length();
			}
			else if (code.find(html_mark, next_pos) == next_pos) {
				// Deal with comments
				// Go until -->
				next_pos = code.find(html_mark_end, next_pos) + html_mark_end.length();
			}
			else if (html_dispose.count(code[next_pos + 1])) {
				// Deal with unused thing: Preventing string,
				bool in_str = false, in_trans = false;
				char ch;
				while ((ch = code[next_pos++]) != html_end || in_str || in_trans) {
					switch (ch) {
					case '\\':
						in_trans = true;
						break;
					case '"':
						if (!in_trans) in_str = !in_str;
						// PASSTHROUGH FOR in_trans SETTER
					default:
						in_trans = false;
					}
				}
				// It's the right place (added) now.
			}
			else {
				// Deal with normal block
				// Should be like: <a id="test" href="javascript:;">
				//                   ----------^ id_pos_end
				//                   ^type_pos
				//                 ^ next_pos

				size_t nadd = next_pos + 1;
				size_t type_pos = code.find(' ', nadd);
				if (type_pos <= next_end_pos) {
					string tag_type = code.substr(nadd, type_pos - nadd);
					string tag_name = "";
					size_t id_pos = code.find(id_matcher, nadd);
					if (id_pos < next_end_pos) {	// Not in the current tag
						size_t id_epos = id_pos + id_matcher.length();
						size_t id_pos_end = code.find(' ', id_epos);
						if (id_pos_end > next_end_pos) id_pos_end = next_end_pos;
						tag_name = code.substr(id_epos, id_pos_end - id_epos);	// "test"

						if (tag_name[0] == '"') {
							// Format in our own
							tag_name.pop_back();
							tag_name.erase(tag_name.begin());
						}
					}
					// Do a preprocess: push into codestream
					
					generateGlobalClass("document." + tag_name, "__element", keep_env);
					keep_env.set_global("document." + tag_name + "._id", intValue(tag_name), true);
					keep_env.set_global("document." + tag_name + "._type", intValue(tag_type), true);
					
					/*
					generateClass("document." + tag_name, "__element", keep_env, false);
					keep_env["document." + tag_name + "._id"] = intValue(tag_name);
					keep_env["document." + tag_name + "._type"] = intValue(tag_type);
					*/
					// Run certain style-reader? (TODO item)

				}
				// Can't be used
				// Always push
				next_pos = next_end_pos + 1;
				
			}

			

		}
	}

	// End
	nenv.preRun(codestream, reqs, { {string("__utfcall"), utf_call} });


	// Getting into the reader...
	auto &ur = reqs["UTF_TARGET"];
	if (!ur.isNull) {
		utf_target = ur.str;
	}
	next_pos = 0;
	size_t previous_pos = 0;
	string current_code = "";
	bool autolen = false, end_of_postback = false;
	while ((next_pos = code.find(blue_start, next_pos)) != string::npos) {
		// Process [previous_pos, next_pos] as normal data
		if (next_pos > previous_pos) content += code.substr(previous_pos, next_pos - previous_pos);
		size_t beginner = next_pos + blue_start.length();	// where .substr(beginner, ...)
		size_t end_pos = code.find(blue_end, beginner);
		bool special = false;
		string firstflag = "";
		if (end_pos == string::npos) {
			raise_global_ce("Unexpected EOF ('?>' required)");
		}
		if (code[beginner] == ':') {
			// Special code ...
			special = true;
			beginner++;
		}
		while (code[beginner] != '\n') {
			char &c = code[beginner++];
			if (c == '\r') continue;
			firstflag += c;
		}
		auto tmp_options = split(firstflag, ' ');
		if (tmp_options.size()) while (tmp_options[0].length() && tmp_options[0][0] == ' ') tmp_options[0].erase(tmp_options[0].begin());
		set<string> options = set<string>(tmp_options.begin(), tmp_options.end());
		beginner++;
		size_t run_size = end_pos - beginner;
		current_code = code.substr(beginner, run_size);
		bool postback_set = false;
		if (options.count("initial")) {
			nenv.preRun(current_code, reqs, { {string("bluecho"), initial_echo}, {string("__utfcall"), utf_call} });
		}
		else if (options.count("autolen")) {
			autolen = true;
		}
		else if (options.count("postback")) {
			/*
			Postback format:
			listen [HTML id].[event like onXXX]
			postback [HTML id].[name] (All of them will become string ...)
			before_send [JS Function] (Only 1 is acceptable)
			after_send [JS Function] (Only 1 is acceptable)

			Event in the code must be like [HTML id]_[event], not using '.'!
			*/
			autolen = true;	// Since postback is used autolen must be used -- <script> will be inserted!
			if ((!reqs.count("SELF_POST")) || reqs["SELF_POST"].isNull) {
				raise_global_ce("SELF_POST is not supported by server");
			}
			string &myself = reqs["SELF_POST"].str;
			while (myself.length() && myself[0] == '"') myself.erase(myself.begin());
			while (myself.length() && myself[myself.length() - 1] == '"') myself.pop_back();
			// Should be provided:
			// Matches 'xhr.setRequestHeader('MinServerPostBack','1');' in the header.
			string &is_postback = reqs["IS_POSTBACK"].str;	// To deal with postback, 0 or 1
			string my_bef_send = "", my_aft_send = "", my_progress = "";
			// Also deal with postback in the field
			if (is_postback == "1") {
				// To be written... serial object into postback support, also send commands back.
				// AND: ANYTHING AFTER IT will be ignored!!!
				nenv.preRun("postback._inside_process", reqs, { {string("bluecho"), normal_echo}, {string("__utfcall"), utf_call} });
				end_of_postback = true;
				break;
			}
			if (postback_set) {
				raise_global_ce("Cannot add 2 or more postback description in a file");
			}
			else {
				postback_set = true;
				vector<string> exprs = split(current_code), curcmd, descmd;
				content += "<script>\n";

				// Add object-liked string for 'onpostback'.
				string onloadcall = "window.onload = function() {\n", onpostback = "function mins_postback(info,para) {\n	var sending = \"__object$\\n.__type__=\\\"object\\\"\\n\";\n";

				// Write JavaScript into content
				for (size_t i = 0; i < exprs.size(); i++) {
					string &cur = exprs[i];
					curcmd = split(cur, ' ', 1);
					if (curcmd.size() < 2) continue;
					if (curcmd[0] == "listen") {
						descmd = split(curcmd[1], '.', 1);
						postback_check(2);
						onloadcall += "	document.getElementById('" + descmd[0] + "')." + descmd[1] + " = function() { mins_postback('" + descmd[0] + "_" + descmd[1] + "'); };\n";
					}
					else if (curcmd[0] == "postback") {
						descmd = split(curcmd[1], '.', 1);
						postback_check(2);
						onpostback += "	sending += '.document." + descmd[0] + "." + descmd[1] + "=\"' + mins_format(document.getElementById('" + descmd[0] + "')." + descmd[1] + ".toString()) + '\"\\n';\n";
					}
					else if (curcmd[0] == "before_send") {
						my_bef_send = curcmd[1];
					}
					else if (curcmd[0] == "after_send") {
						my_aft_send = curcmd[1];
					}
					else if (curcmd[0] == "progressive") {
						my_progress = curcmd[1];
					}
					else if (curcmd[0] == "on_load") {
						onloadcall += "	mins_postback('" + curcmd[1] + "');\n";	
					}
					else {
						raise_global_ce("Bad postback description");
					}
				}

				if (my_progress.length()) onloadcall += "	document.getElementById('" + my_progress + "').style = 'display: none;';\n";
				onloadcall += "\n};";
				onpostback += "\n	if (info != null) sending += '.field=\"' + mins_format(info.toString()) + '\"';\n	if (para != null) sending += '\\n.parameter=\"' + mins_format(para.toString()) + '\"';\n	var xhr = new XMLHttpRequest();\n";
				if (my_progress.length()) onpostback += "	document.getElementById('" + my_progress + "').style = 'display: block;';\n";
				if (my_bef_send.length()) onpostback += my_bef_send + "();\n";
				onpostback += "	setTimeout(function(){\n		xhr.open('POST', '/" + myself +"', true); \n		if (info != null) {xhr.setRequestHeader('MinServerPostBack','1');} \n		xhr.onload = function (e) { if (xhr.readyState == 4) { mins_dealing(xhr.responseText); ";
				if (my_progress.length()) onpostback += "document.getElementById('" + my_progress + "').style = 'display: none;';";
				if (my_aft_send.length()) onpostback += my_aft_send + "(); \n";
				onpostback += "} }\n		xhr.send(sending);\n	}, 0);\n}";
				// Read Postback processor.
				FILE *fread = fopen("Postback.js", "r");
				if (fread == NULL) {
					raise_global_ce("Cannot add JavaScript support of PostBack");
				}
				else {
					while (!feof(fread)) {
						fgets(post_buf1, 8192, fread);
						content += string(post_buf1);// +"\n";
					}
					fclose(fread);
					content += onloadcall;
					content += "\n";
					content += onpostback;

					content += "</script>\n";
				}
			}
		}
		else {
			nenv.preRun(current_code, reqs, { {string("bluecho"), normal_echo} });
		}
		previous_pos = end_pos + blue_end.length();
		next_pos = end_pos;
	}
	while (content.length() && (content[0] == '\n' || content[0] == '\r')) content.erase(content.begin());
	while (header.length() && (header[header.length() - 1] == '\n' || header[header.length() - 1] == '\r')) header.pop_back();
	if (!end_of_postback) content += code.substr(previous_pos);
	FILE *fout = fopen(target_path.c_str(), "w");
	fprintf(fout, "%s\n", header.c_str());
	if (!using_utf) {
		if (autolen) {
			size_t cl = content.length();
			// Add a special patch for windows CR-LF.
			size_t tgt = 0;
			while ((tgt = content.find('\n', tgt)) != string::npos) {
				cl++;	// Add for CR.
				tgt++;	// Length of LF.
			}
			fprintf(fout, "Content-Length: %d\n", cl);
		}
		fprintf(fout, "\n%s", content.c_str());
	}
	else {
		FILE *fcon = fopen(utf_target.c_str(), "w");
		fprintf(fcon, "%s", content.c_str());
		fclose(fcon);
	}
	
	fclose(fout);

	return 0;
}
