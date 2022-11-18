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
using namespace std;

string target_path = "", header = "", content = "";

// To symbol if it's next statement, not next progress
bool np = false, spec_ovrd = false;
int __spec = 0;

// Open if not use bmain.blue
bool no_lib = false;

// Pre declare
class varmap;
struct intValue;
intValue run(string code, varmap &myenv, string fname);
intValue calcute(string expr, varmap &vm);
void raiseError(intValue raiseValue, varmap &myenv, string source_function = "Unknown source", size_t source_line = 0, double error_id = 0, string error_desc = "");
intValue getValue(string single_expr, varmap &vm, bool save_quote = false);

HANDLE stdouth;
DWORD precolor = FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_GREEN, nowcolor = FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_GREEN;
void setColor(DWORD color) {
	SetConsoleTextAttribute(stdouth, color);
	precolor = nowcolor;
	nowcolor = color;
}

inline void begindout() {
	setColor(FOREGROUND_BLUE | FOREGROUND_GREEN | FOREGROUND_INTENSITY);
}

inline void endout() {

	setColor(precolor);
}

inline void specialout() {
	setColor(FOREGROUND_GREEN | FOREGROUND_INTENSITY);
}

inline void curlout() {
	setColor(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY);
}

const int max_indent = 65536;

char buf0[255], buf01[255], buf1[65536];
bool in_debug = false;	// Runner debug option.
//set<size_t> breakpoints;
vector<string> watches;

// It's so useful that everyone needs it
bool beginWith(string origin, string judger) {
	return origin.length() >= judger.length() && origin.substr(0, judger.length()) == judger;
}

// At most delete maxfetch.
int getIndent(string &str, int maxfetch = -1) {
	int id = 0;
	while (str.length() && str[0] == '\n') str.erase(str.begin());
	while (str.length() && str[0] == '\t' && id != maxfetch) {
		id++;
		str.erase(str.begin());
	}
	return id;
}

int getIndentRaw(string str, int maxfetch = -1) {
	string s = str;
	return getIndent(s, maxfetch);
}

// quotes and dinner will be reserved
// N maxsplit = N+1 elements. -1 means no maxsplit
vector<string> split(string str, char delimiter = '\n', int maxsplit = -1, char allowedquotes = '"', char allowedinner = -1, bool reserve_dinner = false) {
	// Manually breaks
	bool qmode = false, dmode = false;
	vector<string> result;
	if (maxsplit > 0) result.reserve(maxsplit);
	string strtmp = "";
	for (size_t i = 0; i < str.length(); i++) {
		char &cs = str[i];
		if (cs == allowedquotes && (!dmode)) {
			qmode = !qmode;
		}
		if (cs == allowedinner && (!dmode)) {
			dmode = true;
			if (reserve_dinner) strtmp += cs;
		}
		else {
			dmode = false;
			if (cs == delimiter && (!qmode) && strtmp.length() && result.size() != maxsplit) {
				result.push_back(strtmp);
				strtmp = "";
			}
			else {
				strtmp += cs;
			}
		}
	}
	if (strtmp.length()) result.push_back(strtmp);
	return result;
}

string unformatting(string origin) {
	string ns = "\"";
	for (size_t i = 0; i < origin.length(); i++) {
		if (origin[i] == '"' || origin[i] == '\\') {
			ns += "\\";
			ns += origin[i];
		}
		else {
			ns += origin[i];
		}
	}
	return ns + "\"";
}

struct intValue {
	// To be considered:
	// When these values are set, 'isNull' should NOT be true anymore.

	// DO NOT WRITE THEM DIRECTLY!
	double								numeric;
	string								str;
	bool								isNull = false;
	bool								isNumeric = false;

	intValue negative() {
		if (this->isNumeric) {
			return intValue(-this->numeric);
		}
		else if (!this->isNull) {
			return intValue(this->str);
		}
		else {
			return null;
		}
	}

	void set_numeric(double value) {
		this->numeric = value;
		this->isNull = false;
		this->isNumeric = true;
	}

	void set_str(string value) {
		this->str = value;
		this->isNull = false;
		this->isNumeric = true;
	}

	intValue() {
		isNull = true;
		numeric = 0;
		str = "";
	}
	intValue(double numeric) : numeric(numeric) {
		isNumeric = true;
		sprintf(buf0, "%lf", numeric);
		str = buf0;
		while (str.back() == '0') str.pop_back();
		if (str.back() == '.') str.pop_back();
	}
	intValue(string str) : str(str) {

	}

	// Usually use for debug proposes
	void output() {
		if (isNull) {
			cout << "null";
		}
		else if (isNumeric) {
			cout << "num:" << numeric;
		}
		else {
			cout << "str:" << str;
		}
	}

	string unformat() {
		if (isNull) {
			return "null";
		}
		else if (isNumeric) {
			sprintf(buf01, "%lf", numeric);
			return buf01;
		}
		else {
			// Unformat this string
			return unformatting(str);
		}
	}

	bool boolean() {
		if (isNull) return false;
		if (isNumeric)
			if (this->numeric == 0) return false;
		//return true;
		if (this->str.length()) return true;
		return false;
	}

} null;

#define raise_ce(description) raiseError(null, myenv, fname, execptr, __LINE__, description)
#define raise_varmap_ce(description) raiseError(null, *this, "Runtime", 0, __LINE__, description)
#define raise_gv_ce(description) raiseError(null, vm, "Runtime", 0, __LINE__, description);
#define raise_global_ce(description) do { varmap v; v.push(); raiseError(null, v, "BluePage Interpreter", 0, __LINE__, description); } while (false)

inline int countOf(string str, char charToFind) {
	int count = 0;
	for (auto &i : str) {
		if (i == charToFind) count++;
	}
	return count;
}

// All the varmaps show one global variable space.
/*
Special properties:

__arg__			For a function, showing its parameters (delimitered by space).
__init__		For a class definition, showing its initalizing function.
	For class,	[name].[function]
__type__		For a class object, showing its kind.
__inherits__	For a class object, showing its inherited class (split using ','.)
__hidden__		For a class definition, showing if this value will be hidden during get_member (if 'forceShow' is not given).

Extras:
__must_inherit__		For a class objct, 1 if it must be inherited.
	(A __must_inherit__ = 1 class can't have object, either.)
__no_inherit__			For a class object, 1 if it mustn't be inherited.
__shared__				For a class object, 1 if object can't create from the class (shared class).
	(This feature will be inherited!)
	For a function object, 1 if this function can't use "this" but can be called directly by class name.

In environment:
__error_handler__			(User define) processes error handler
__is_sharing__				(User define) symbol if is calling shared thing.
	(If __is_sharing__ = 1, call of "this" will fail.)

ALL THINGS ABOVE WILL BECOME A STRING

Therefore, the result of serial() should be considered!
Functions will become STRING as well!

The "null" inside should be seen as 'null' (which has .isNull = true).

*/
// Specify what will not be copied.
const set<string> nocopy = { ".__type__", ".__inherits__", ".__arg__", ".__must_inherit__", ".__no_inherit__" };
// Specify what will not be lookup.
const set<string> magics = { ".__type__", ".__inherits__", ".__arg__", ".__must_inherit__", ".__no_inherit__", ".__init__", ".__hidden__", ".__shared__" };
class varmap {
public:

	using value_type = intValue;
	using single_mapper = map<string, value_type>;

	typedef vector<single_mapper>::reverse_iterator			vit;
	typedef single_mapper::iterator							mit;

	varmap() {

	}
	void push() {
		vs.push_back(single_mapper());
	}
	void pop() {
		if (vs.size()) vs.pop_back();
	}
	bool count(string key) {
		if (key == "this") {
			if (this_name.length() && this_source != NULL) return true;
			else return false;
		}
		else if (beginWith(key, "this.")) {
			vector<string> s = split(key, '.', 1);
			return this_source->count(this_name + "." + s[1]);
		}
		else {
			for (vit i = vs.rbegin(); i != vs.rend(); i++) {
				if (i->count(key)) {
					return true;
				}
			}
			if (glob_vs.count(key)) return true;
			return false;
		}
	}
	// If return object serial, DON'T MODIFY IT !
	value_type& operator[](string key) {
#pragma region Debug Purpose
		//cout << "Require key: " << key << endl;
#pragma endregion
			// Shouldn't be LF in key.
		for (size_t i = 0; i < key.length(); i++) {
			if (key[i] == '\n') key.erase(key.begin() + i);
		}
		// Find where it is
		bool is_sharing = false;
		if (key != "__is_sharing__" && this->operator[]("__is_sharing__").str == "1") {
			is_sharing = true;
		}
		if (key == "this") {
			// Must be class
			if (is_sharing || (this_source == NULL)) {
				raise_varmap_ce("Error: attempt to call 'this' in a shared function or non-class function");
			}
			return (*this_source)[this_name] = this_source->serial(this_name);
		}
		else if (key.substr(0, 5) == "this.") {
			if (is_sharing || (this_source == NULL)) {
				curlout();
				raise_varmap_ce("Error: attempt to call 'this' in a shared function or non-class function");
				endout();
			}
			vector<string> s = split(key, '.', 1);
			return (*this_source)[this_name + "." + s[1]];
		}
		else {
			for (vit i = vs.rbegin(); i != vs.rend(); i++) {
				if (i->count(key)) {
					if (unserial.count((*i)[key + ".__type__"].str)) {
						return ((*i))[key];
					}
					else {
						return ((*i))[key] = serial(key);
					}
				}
			}

			if (glob_vs.count(key)) {
				if (unserial.count(glob_vs[key + ".__type__"].str)) {
					return glob_vs[key];
				}
				else {
					return glob_vs[key] = serial(key);
				}
			}
			if (key.find('.') != string::npos) {
				// Must in same layer
				vector<string> la = split(key, '.', 1);
				for (vit i = vs.rbegin(); i != vs.rend(); i++) {
					if (i->count(la[0])) {
						(*i)[key] = null;
						return (*i)[key];
					}
				}
				if (glob_vs.count(la[0])) {
					glob_vs[key] = null;
					return glob_vs[key];
				}
			}
			else {
				if (!vs[vs.size() - 1].count(key)) vs[vs.size() - 1][key] = null;
			}
			return vs[vs.size() - 1][key];
		}

	}
	value_type serial(string name) {
		for (vit i = vs.rbegin(); i != vs.rend(); i++) {
			if (i->count(name)) {
				return serial_from(*i, name);
			}
		}
		if (glob_vs.count(name)) {
			return serial_from(glob_vs, name);
		}
		return mymagic;
	}
	vector<value_type> get_member(string name, bool force_show = false) {
		for (vit i = vs.rbegin(); i != vs.rend(); i++) {
			if (i->count(name)) {
				return get_member_from(*i, name, force_show);
			}
		}
		if (glob_vs.count(name)) {
			return get_member_from(glob_vs, name, force_show);
		}
		return vector<value_type>();
	}
	void deserial(string name, string serial) {
		if (!beginWith(serial, mymagic)) {
			return;
		}
		serial = serial.substr(mymagic.length());
		vector<string> lspl = split(serial, '\n', -1, '\"', '\\', true);
		for (auto &i : lspl) {
			vector<string> itemspl = split(i, '=', 1);
			if (itemspl.size() < 2) itemspl.push_back("null");
			(*this)[name + itemspl[0]] = getValue(itemspl[1], *this);
		}
		(*this)[name] = null;
	}
	void tree_clean(string name) {
		if (name == "this") {
			this->this_source = NULL;
			this->this_name = "";
		}
		else if (beginWith(name, "this.")) {
			vector<string> spl = split(name, '.', 1);
			if (spl.size() < 2) return;
			this->this_source->tree_clean(this->this_name + "." + spl[1]);
		}
		else {
			// Clean in my tree.
			for (auto i = vs.rbegin(); i != vs.rend(); i++) {
				if (i->count(name)) {
					(*i)[name] = null;
					vector<string> to_delete;
					for (auto &j : (*i)) {
						if (beginWith(j.first, name + ".")) {
							// Delete!
							to_delete.push_back(j.first);
						}
					}
					for (auto &j : to_delete) {
						i->erase(j);
					}
				}
			}
		}
	}
	static void set_global(string key, value_type value) {
		glob_vs[key] = value;
	}
	static void declare_global(string key) {
		set_global(key, null);
	}
	void declare(string key) {
		vs[vs.size() - 1][key] = null;
	}
	void set_this(varmap *source, string name) {
		this_name = name;
		this_source = source;
	}
	void dump() {
		specialout();
		cout << "*** VARMAP DUMP ***" << endl;
		cout << "this pointer: " << this_name << endl << "partial:" << endl;
		for (vit i = vs.rbegin(); i != vs.rend(); i++) {
			for (mit j = i->begin(); j != i->end(); j++) {
				cout << j->first << " = ";
				j->second.output();
				cout << endl;
			}
			cout << endl;
		}
		cout << "global:" << endl;
		for (mit i = glob_vs.begin(); i != glob_vs.end(); i++) {
			cout << i->first << " = ";
			i->second.output();
			cout << endl;
		}
		cout << "*** END OF DUMP ***" << endl;
		endout();
	}
	static void copy_inherit(string from, string dest) {
		while (from[from.length() - 1] == '\n') from.pop_back();
		while (dest[dest.length() - 1] == '\n') dest.pop_back();
		for (auto &i : glob_vs) {
			if (beginWith(i.first, from + ".")) {
				bool flag = false;
				for (auto tests : nocopy) {
					if (beginWith(i.first, from + tests)) flag = true;
				}
				if (flag) continue;
				vector<string> spl = split(i.first, '.', 1);
				glob_vs[dest + "." + spl[1]] = i.second;
				glob_vs[dest + "." + from + "@" + spl[1]] = i.second;
			}
		}
	}

	string															this_name = "";
	varmap															*this_source;
	// Where 'this' points. use '.'
	const string mymagic = "__object$\n";
private:
	vector<value_type> get_member_from(single_mapper &obj, string name, bool force_show = false) {
		vector<value_type> result;
		string mytype = (*this)[name + ".__type__"].str;
		if (unserial.count(mytype)) mytype = "";
		for (auto &i : obj) {
			// Only lookup for one
			size_t dpos = i.first.find_first_of('.');
			if (dpos >= i.first.length()) continue;
			string keyname = i.first.substr(dpos);
			if (countOf(i.first, '.') == 1) {
				bool isshown = true;
				if ((!force_show) && (mytype.length())) {
					for (auto &j : magics) {
						if (i.first.find(j) != string::npos) {
							isshown = false;
							break;
						}
					}
					if (isshown) {
						string hiddener = mytype + keyname + ".__hidden__";
						if ((*this)[hiddener].str == "1") isshown = false;
					}
				}
				if (force_show || isshown) {
					result.push_back(intValue(keyname.substr(1)));
				}
			}
		}
		return result;
	}
	intValue serial_from(single_mapper &obj, string name) {
		string tmp = mymagic;
		for (auto &j : obj) {
			if (beginWith(j.first, name + ".")) {
				//vector<string> spl = split(j.first, '.', 1);
				vector<string> spl = { "","" };
				size_t fl = j.first.find('.', j.first.find(name) + name.length());
				if (fl == string::npos) continue;
				spl[0] = j.first.substr(0, fl);
				if (spl[0] != name) continue;
				spl[1] = j.first.substr(fl + 1);
				tmp += string(".") + spl[1] + "=" + j.second.unformat() + "\n";
			}
		}
		return intValue(tmp);
	}
	const set<string> unserial = { "", "function", "class", "null" };

	vector<map<string, value_type> >										vs;
	// Save evalable thing, like "" for string
	static map<string, value_type>										glob_vs;
};

map<string, varmap::value_type> varmap::glob_vs;

void raiseError(intValue raiseValue, varmap &myenv, string source_function, size_t source_line, double error_id, string error_desc) {
	if (source_function == "__error_handler__") {
		curlout();
		cout << "During processing error, another error occured:" << endl;
		cout << "Line: " << source_line << endl;
		cout << "Error #: " << error_id << endl;
		cout << "Description: " << error_desc << endl;
		cout << "System can't process error in error handler. System will quit." << endl;
		exit(1);
		endout();
		return;
	}
	myenv.set_global("err.value", raiseValue);
	myenv.set_global("err.source", intValue(source_function));
	myenv.set_global("err.line", intValue(source_line));
	myenv.set_global("err.id", intValue(error_id));
	myenv.set_global("err.description", intValue(error_desc));
	varmap emer_var;
	emer_var.push();
	run(myenv["__error_handler__"].str, emer_var, "__error_handler__");
}

class inheritance_disjoint {
public:
	inheritance_disjoint() {

	}
	string find(string name) {
		while (name.length() && name[name.length() - 1] == '\n') name.pop_back();
		if (!inhs.count(name)) inhs[name] = name;
		if (inhs[name] == name) return name;
		return inhs[name] = find(inhs[name]);
	}
	inline void unions(string a, string b) {
		inhs[find(a)] = find(b);
	}
	inline bool is_same(string a, string b) {
		return find(a) == find(b);
	}
private:
	map<string, string> inhs;
} inh_map;

// Ignorer can be quote-like char.
string formatting(string origin, char dinner = '\\', char ignorer = -1) {
	string ns = "";
	bool dmode = false, ign = false;
	for (size_t i = 0; i < origin.length(); i++) {
		if (origin[i] == ignorer && (!dmode)) {
			ign = !ign;
		}
		if (origin[i] == dinner && (!dmode) && (!ign)) {
			dmode = true;
		}
		else {
			ns += origin[i];
			dmode = false;
		}
	}
	return ns;
}

// If save_quote, formatting() will not process anything inside quote.
intValue getValue(string single_expr, varmap &vm, bool save_quote) {
	if (single_expr == "null" || single_expr == "") return null;
	// Remove any '(' in front
	while (single_expr.length() && single_expr[0] == '(') {
		if (single_expr[single_expr.length() - 1] == ')') single_expr.pop_back();
		single_expr.erase(single_expr.begin());
	}
	if (single_expr[0] == '"' && single_expr[single_expr.length() - 1] == '"') {
		return formatting(single_expr.substr(1, single_expr.length() - 2), '\\', (save_quote ? '\"' : char(-1)));
	}
	// Is number?
	bool dot = false, isnum = true;
	double neg = 1;
	for (size_t i = 1; i < single_expr.length(); i++) {	// Not infl.
		char &ch = single_expr[i];
		if (ch == '.') {
			if (dot) {
				// Not a number
				isnum = false;
				break;
			}
			else {
				dot = true;
			}
		}
		else if (ch < '0' || ch > '9') {
			isnum = false;
			break;
		}
	}
	char &fch = single_expr[0];
	if (fch < '0' || fch > '9') {
		if (fch == '-') {
			neg = -1;
			single_expr.erase(single_expr.begin());
		}
		else {
			isnum = false;
		}
	}
	if (isnum) {
		return atof(single_expr.c_str()) * neg;
	}
	else {
		vector<string> spl = split(single_expr, ' ', 1);
		// Neither string nor number, variable test
		//vector<string> dotspl = split(spl[0], '.', 1);
		// Must find last actually.
		if (spl.size() && spl[0].length() && spl[0][0] == '$') {
			spl[0] = vm[spl[0].substr(1)].str;
		}
		vector<string> dotspl = { "","" };
		size_t fl = spl[0].find_last_of('.');
		if (fl >= string::npos || fl + 1 >= spl[0].length()) {	// string::npos may overrides
			dotspl[0] = spl[0];
			dotspl.pop_back();
		}
		else {
			dotspl[0] = spl[0].substr(0, fl);
			dotspl[1] = spl[0].substr(fl + 1);
		}
		string set_this = "";
		bool set_no_this = false, is_static = false;
		bool class_obj = false;
		if (dotspl.size() > 1 && !vm[dotspl[0] + ".__type__"].isNull && vm[dotspl[0] + ".__type__"].str != "function") {
			class_obj = true;
			if (vm[dotspl[0] + ".__type__"].str == "class" && (vm[dotspl[0] + ".__shared__"].str == "1" || vm[spl[0] + ".__shared__"].str == "1")) {
				// Do nothing!
				set_no_this = true;
				is_static = true;
			}
			else {
				set_this = dotspl[0];

			}

		}
		bool is_func = false;
		if (class_obj) {
			//if (class_obj) spl[0] = vm[dotspl[0] + ".__type__"] + "." + dotspl[1];	// Call the list function.
			string header;
			if (is_static) {
				header = dotspl[0];
			}
			else {
				header = vm[dotspl[0] + ".__type__"].str;
			}
			string tmp = header + "." + dotspl[1];
			if (vm[tmp + ".__type__"].str == "function") {
				is_func = true;
				spl[0] = tmp;
			}
		}
		else {
			is_func = vm[spl[0] + ".__type__"].str == "function";
		}
		if (is_func) {
			// A function call.

			varmap nvm;
			nvm.push();
			nvm.set_this(vm.this_source, vm.this_name);
			if (spl[0].find('.') != string::npos && (!set_no_this)) {
				vector<string> xspl = split(spl[0], '.');
				nvm.set_this(&vm, xspl[0]);
				xspl[0] = vm[spl[0] + ".__type__"].str;
			}
			string args = vm[spl[0] + ".__arg__"].str;
			if (args.length()) {
				vector<string> argname = split(args, ' ');
				vector<string> arg;
				vector<intValue> ares;
				bool str = false, dmode = false;
				if (spl.size() >= 2) {
					//arg = split(spl[1], ',');
					int quotes = 0;
					string tmp = "";
					for (auto &i : spl[1]) {
						if (i == '(') {
							if (quotes) tmp += i;
							quotes++;
						}
						else if (i == ')') {
							quotes--;
							if (quotes) tmp += i;
						}
						else if (i == '\\' && (!dmode)) {
							dmode = true;
							tmp += i;
						}
						else if (i == '"' && (!dmode)) {
							str = !str;
							tmp += i;
						}
						else if (i == ',' && (!quotes) && (!str)) {
							arg.push_back(tmp);
							tmp = "";
						}
						else {
							tmp += i;
						}
						dmode = false;
					}
					if (tmp.length()) arg.push_back(tmp);
				}
				else {
					arg.push_back(spl[1]);
				}
				if (arg.size() != argname.size()) {
					raise_gv_ce(string("Warning: Parameter dismatches or function does not exist while calling function ") + spl[0]);

					return null;
				}
				for (size_t i = 0; i < arg.size(); i++) {
					nvm[argname[i]] = calcute(arg[i], vm);
				}
			}
			if (set_this.length()) nvm.set_this(&vm, set_this);
			if (set_no_this) {
				nvm["__is_sharing__"].set_str("1");
			}
			string s = vm[spl[0]].str;
			if (vm[spl[0]].isNull || s.length() == 0) {
				raise_gv_ce(string("Warning: Call of null function ") + spl[0]);
			}
			__spec++;
			auto r = run(s, nvm, spl[0]);
			__spec--;
			if (r.isNumeric && neg < 0) {
				r = intValue(-r.numeric);
			}
			return r;
		}
		else {
			auto r = vm[spl[0]];
			if (r.isNumeric && neg < 0) {
				r = intValue(-r.numeric);
			}
			return r;
		}
		// So you have refrences
	}
}

int priority(char op) {
	switch (op) {
	case ')': case ':':	// Must make sure ':' has the most priority
		return 6;
		break;
	case '#':
		return 5;
		break;
	case '&': case '|':
		return 4;
		break;
	case '*': case '/': case '%':
		return 3;
		break;
	case '+': case '-':
		return 2;
		break;
	case '>': case '<': case '=':
		return 1;
		break;
	case '(':
		return 0;
		break;
	default:
		return -1;
	}
}

typedef long long							long64;
typedef unsigned long long					ulong64;

// Please notice special meanings.
intValue primary_calcute(intValue first, char op, intValue second, varmap &vm) {
	switch (op) {
	case '(': case ')':
		break;
	case ':':
		// As for this, 'first' should be direct var-name
		return vm[first.str + "." + second.str];
		break;
	case '#':
		// To get a position for string, or power for integer.
		if (first.isNumeric) {
			return pow(first.numeric, second.numeric);
		}
		else {
			ulong64 ul = ulong64(second.numeric);
			if (ul >= first.str.length()) {
				return null;
			}
			else {
				return string({ first.str[ul] });
			}
		}
		break;
	case '*':
		if (first.isNumeric) {
			return first.numeric * second.numeric;
		}
		else {
			string rep = "";
			for (int cnt = 0; cnt < second.numeric; cnt++) {
				rep += first.str;
			}
			return rep;
		}
		break;
	case '%':
		if (second.numeric == 0) {
			return null;
		}
		return long64(first.numeric) % long64(second.numeric);
		break;
	case '/':
		if (second.numeric == 0) {
			return null;
		}
		return first.numeric / second.numeric;
		break;
	case '+':
		if (first.isNumeric && second.isNumeric) {
			return first.numeric + second.numeric;
		}
		else {
			return first.str + second.str;
		}
		break;
	case '-':
		return first.numeric - second.numeric;
		break;
	case '>':
		if (first.isNumeric) {
			return first.numeric > second.numeric;
		}
		else {
			return first.str > second.str;
		}
		break;
	case '<':
		if (first.isNumeric) {
			return first.numeric < second.numeric;
		}
		else {
			return first.str < second.str;
		}
		break;
	case '=':
		if (first.isNull && second.isNull) {
			return true;
		}
		if (first.isNumeric) {
			return first.numeric == second.numeric;
		}
		else {
			return first.str == second.str;
		}
		break;
	case '&':
		if (first.isNumeric && second.isNumeric) {
			return ulong64(first.numeric) & ulong64(second.numeric);
		}
		else {
			if (first.isNull || second.isNull) return 0;
			if (first.str.length() == 0 || second.str.length() == 0) return 0;
			return 1;
		}
		break;
	case '|':
		if (first.isNumeric && second.isNumeric) {
			return ulong64(first.numeric) | ulong64(second.numeric);
		}
		else {
			if (first.isNull && second.isNull) return 0;
			if (first.str.length() == 0 && second.str.length() == 0) return 0;
			return 1;
		}
		break;
	default:
		return null;
	}
}

// Code must be checked
intValue calcute(string expr, varmap &vm) {
	if (expr.length() == 0) return null;
	stack<char> op;
	stack<intValue> val;
	string operand = "";
	bool cur_neg = false, qmode = false, dmode = false;
	int ignore = 0, op_pr;

	auto auto_push = [&]() {
		if (cur_neg) {
			val.push(getValue(operand, vm).negative());
		}
		else {
			val.push(getValue(operand, vm));
		}
		cur_neg = false;
	};

	// my_pr not provided (-1): Keep on poping
	auto auto_pop = [&](int my_pr = -1) {
		op_pr = -2;
		while ((!op.empty()) && (op_pr = priority(op.top())) > my_pr) {
			intValue v1, v2;
			char mc = op.top();
			op.pop();
			v1 = val.top();


			val.pop();
			v2 = val.top();
			val.pop();
			intValue pres = primary_calcute(v2, mc, v1, vm);
			val.push(pres);
		}
	};

	for (size_t i = 0; i < expr.length(); i++) {
		if (expr[i] == '"' && (!dmode)) qmode = !qmode;
		if (expr[i] == '\\' && (!dmode)) dmode = true;
		else dmode = false;
		int my_pr = priority(expr[i]);
		if (my_pr >= 0 && (!qmode) && (!dmode)) {
			if (expr[i] == '(') {
				// Here should be operator previously.
				int t = 1;
				while (int(i) - t >= 0 && expr[i - t] == '(') t--;
				if ((i == 0 || priority(expr[i - t]) >= 0) && (!ignore)) {
					op.push('(');
				}
				else {
					ignore++;
					operand += expr[i];
				}
			}
			else if (expr[i] == ')') {
				if (ignore <= 0) {
					if (operand.length()) {
						auto_push();
					}
					while ((!op.empty()) && (op.top() != '(')) {
						intValue v1, v2;
						char mc = op.top();
						op.pop();

						v1 = val.top();

						val.pop();
						v2 = val.top();

						val.pop();
						intValue pres = primary_calcute(v2, mc, v1, vm);
						val.push(pres);
					}
					op.pop();	// '('
					operand = "";
				}
				else {
					ignore--;
					operand += expr[i];
				}

			}
			else if (ignore <= 0) {
				// May check here.
				if (expr[i] == '-' && (i == 0 || expr[i - 1] == '(' || expr[i - 1] == ',' || expr[i - 1] == ' ')) {
					if (i > 0 && (expr[i - 1] == ',' || expr[i - 1] == ' ')) {
						// It's the beginning of function parameters!
						operand += expr[i];
					}
					else {
						cur_neg = true;
					}
				}
				else {
					if (operand.length()) {
						if (expr[i] == ':') {
							// Must be a raw, existing, indexable thing.
							if (!vm.count(operand)) {
								raise_gv_ce(string("Error: not a variable: ") + operand);
								return null;	// Bad expression!
							}
							val.push(intValue(operand));
						}
						else {
							auto_push();
						}
						cur_neg = false;
					}
					auto_pop(my_pr);
					op.push(expr[i]);
					operand = "";
				}
			}
			else {
				operand += expr[i];
			}

		}
		else {
			operand += expr[i];
		}
	}
	if (operand.length()) {
		auto_push();
		cur_neg = false;
	}
	auto_pop();
	return val.top();
}

#define parameter_check(req) do {if (codexec.size() < req) {raise_ce("Error: required parameter not given") ;return null;}} while (false)
#define parameter_check2(req,ext) do {if (codexec2.size() < req) {raise_ce(string("Error: required parameter not given in sub command ") + ext); return null;}} while (false)
#define parameter_check3(req,ext) do {if (codexec3.size() < req) {raise_ce(string("Error: required parameter not given in sub command ") + ext); return null;}} while (false)
#define dshell_check(req) do {if (spl.size() < req) {cout << "Bad command" << endl; goto dend;}} while (false)
#define postback_check(req) do {if (descmd.size() < req) {raise_global_ce("Error: required parameter not given in postback settings"); }} while (false)

string curexp(string exp, varmap &myenv) {
	vector<string> dasher = split(exp, ':');
	if (dasher.size() == 1) return exp;
	// calcute until the last.
	intValue final = calcute(dasher[dasher.size() - 1], myenv);
	for (size_t i = dasher.size() - 2; i >= 1; i--) {
		final = calcute(dasher[i] + ":" + final.str, myenv);
	}
	return dasher[0] + "." + final.str;
}

map<int, FILE*> files;

class jumpertable {
public:

	jumpertable() {

	}

	size_t& operator [](size_t origin) {
		//if (origin == 11) {
		//	cout << "*Developer Debugger*" << endl;
		//}
		if ((!jmper.count(origin)) || jmper[origin].size() < 1) {
			jmper[origin] = vector<size_t>({ UINT16_MAX });
			return jmper[origin][0];
		}
		else {
			//return jmper[origin][jmper[origin].size() - 1];
			jmper[origin].push_back(jmper[origin][jmper[origin].size() - 1]);
			return jmper[origin][jmper[origin].size() - 1];
		}
	}

	bool revert(size_t origin, size_t revert_from, bool omit = false) {
		bool flag = false;
		while (jmper[origin].size() && jmper[origin][jmper[origin].size() - 1] == revert_from) {
			jmper[origin].pop_back();
			flag = true;
		}
		if (flag && (!omit)) {
			reverted[origin] = revert_from;
		}
		return flag;
	}

	bool revert_all(size_t revert_from, bool omit = false) {
		clear_revert();
		bool flag = false;
		for (auto &i : jmper) {
			flag |= revert(i.first, revert_from, omit);
		}
		return flag;
	}

	void rollback() {
		for (auto &i : reverted) {
			jmper[i.first].push_back(i.second);
		}
		clear_revert();
	}

	void clear_revert() {
		reverted = map<size_t, size_t>();
	}

	bool count(size_t origin) {
		return jmper.count(origin) && jmper[origin].size();
	}

private:
	map<size_t, vector<size_t> > jmper;
	map<size_t, size_t> reverted;
};

typedef intValue(*bcaller)(string, varmap&);
map<string, bcaller> intcalls;

int random() {
	static int seed = time(NULL);
	srand(seed);
	return seed = rand();
}

inline bool haveContent(string s, char filter = '\t') {
	for (auto &i : s) if (i != filter) return true;
	return false;
}

size_t getLength(int fid) {
	size_t cur = ftell(files[fid]);
	fseek(files[fid], 0, SEEK_END);
	size_t res = ftell(files[fid]);
	fseek(files[fid], cur, SEEK_SET);
	return res;
}

void generateClass(string variable, string classname, varmap &myenv, bool run_init = true) {
	myenv[variable] = null;
	myenv[variable + ".__type__"] = classname;
	if (run_init) {
		varmap vm;
		vm.push();
		vm.set_this(&myenv, variable);
		__spec++;
		string cini = classname + ".__init__";
		run(myenv[cini].str, vm, cini);
		__spec--;
	}
}

string env_name;	// Directory of current file.

const set<char> to_trim = { ' ', '\n', '\r', -1 };

// This 'myenv' must be pushed
intValue preRun(string code, varmap &myenv, map<string, string> required_global = {}, map<string, bcaller> required_callers = {}) {
	// Should prepare functions for it.
	string fname = "Runtime preproessor";
	myenv.push();
	// Preset constants
#pragma region Preset constants
	myenv.set_global("LF", intValue("\n"));
	myenv.set_global("TAB", intValue("\t"));
	myenv.set_global("BKSP", intValue("\b"));
	myenv.set_global("ALERT", intValue("\a"));
	myenv.set_global("CLOCKS_PER_SEC", intValue(CLOCKS_PER_SEC));
	myenv.set_global("true", intValue(1));
	myenv.set_global("false", intValue(0));
	myenv.set_global("err.__type__", intValue("exception"));			// Error information
	myenv.set_global("__error_handler__", intValue("call set_color,14\nprint err.description+LF+err.value+LF\ncall set_color,7"));	// Preset error handler
	myenv.set_global("__file__", intValue(env_name));
	// Insert more global variable
	for (auto &i : required_global) {
		myenv.set_global(i.first, i.second);
	}
#pragma endregion
#pragma region Preset calls
	intcalls["sleep"] = [](string args, varmap &env) -> intValue {
		Sleep(DWORD(calcute(args, env).numeric));
		return null;
	};
	intcalls["system"] = [](string args, varmap &env) -> intValue {
		system(calcute(args, env).str.c_str());
		return null;
	};
	intcalls["exit"] = [](string args, varmap &env) -> intValue {
		exit(int(calcute(args, env).numeric));
		return null;
	};
	intcalls["set_color"] = [](string args, varmap &env) -> intValue {
		setColor(DWORD(calcute(args, env).numeric));
		return null;
	};
	// Remind you that eval is dangerous!
	intcalls["eval"] = [](string args, varmap &env) -> intValue {
		return run(calcute(args, env).str, env, "Internal eval()");
	};
	// It is better to add more functions by intcalls, not set
#define math_extension(funame) intcalls["_maths_" #funame] = [](string args, varmap &env) -> intValue { \
		return intValue(funame(calcute(args, env).numeric)); \
	}
	math_extension(sin);
	math_extension(cos);
	math_extension(tan);
	math_extension(asin);
	math_extension(acos);
	math_extension(atan);
	math_extension(sqrt);
	intcalls["_trim"] = [](string args, varmap &env) -> intValue {
		string s = calcute(args, env).str;
		while (s.length() && to_trim.count(s[s.length() - 1])) s.pop_back();
		size_t spl;
		for (spl = 0; spl < s.length(); spl++) if (!to_trim.count(s[spl])) break;
		return intValue(s.substr(spl));
	};
	for (auto &i : required_callers) {
		intcalls[i.first] = i.second;
	}
#pragma endregion
	// Get my directory
	size_t p = env_name.find_last_of('\\');
	string env_dir;	// Directly add file name.
	if (p <= 0) {
		env_dir = "";
	}
	else {
		env_dir = env_name.substr(0, p) + '\\';
	}
	vector<string> codestream;
	// Initalize libraries right here
	FILE *f = fopen("bmain.blue", "r");
	if (f != NULL) {
		while (!feof(f)) {
			fgets(buf1, 65536, f);
			codestream.push_back(buf1);
		}
	}
	fclose(f);
	// End

	vector<string> sc = split(code, '\n', -1, '\"', '\\', true);
	codestream.insert(codestream.end() - 1, sc.begin(), sc.end());
	string curclass = "";					// Will append '.'
	string curfun = "", cfname = "", cfargs = "";
	int fun_indent = max_indent;
	for (size_t i = 0; i < codestream.size(); i++) {
		size_t &execptr = i;	// For macros
		vector<string> codexec = split(codestream[i], ' ', 2);
		if (codexec.size() <= 0 || codexec[0].length() <= 0) continue;
		int ind = getIndent(codexec[0], 2);
		if (codexec[0].length() && codexec[0][codexec[0].length() - 1] == '\n') codexec[0].pop_back();
		if (ind >= fun_indent) {
			string s = codestream[i];
			getIndent(s, fun_indent);
			curfun += s;
			curfun += '\n';
		}
		else {
			if (cfname.length()) {
				myenv.set_global(cfname, intValue(curfun));
				myenv.set_global(cfname + ".__type__", intValue("function"));
				myenv.set_global(cfname + ".__arg__", cfargs);
			}

			cfname = "";
			curfun = "";
			fun_indent = max_indent;
			cfargs = "";
			switch (ind) {
			case 0:
				// Certainly getting out
				curclass = "";
				/*
				class [name]:
					init:
						...
					function [name] [arg ...]:
						...
				*/
				if (codexec[0] == "class") {
					parameter_check(2);
					if (codexec[1][codexec[1].length() - 1] == '\n') codexec[1].pop_back();
					codexec[1].pop_back();	// ':'
					myenv.set_global(codexec[1] + ".__type__", intValue("class"));
					myenv.declare_global(codexec[1]);
					curclass = codexec[1] + ".";
				}
				else if (codexec[0] == "import") {
					parameter_check(2);
					vector<string> codexec2 = split(codestream[i], ' ', 1);
					FILE *f = fopen(codexec2[1].c_str(), "r");
					if (f != NULL) {
						while (!feof(f)) {
							fgets(buf1, 65536, f);
							codestream.push_back(buf1);
						}
					}
					else {
						f = fopen((env_dir + codexec2[1]).c_str(), "r");
						if (f != NULL) {
							while (!feof(f)) {
								fgets(buf1, 65536, f);
								codestream.push_back(buf1);
							}
						}
					}
				}
				else if (codexec[0] == "error_handler:") {
					fun_indent = 1;
					cfname = "__error_handler__";
				}
				break;
			case 1:
				if (codexec[0] == "init:") {
					fun_indent = 2;
					cfname = curclass + "__init__";
				}
				else if (codexec[0] == "inherits") {
					string &to_inherit = codexec[1];
					if (myenv[to_inherit + ".__no_inherit__"].str == "1") {
						curlout();
						cout << "Warning: No inheriting class " << to_inherit << endl;
						endout();
					}
					else {
						string curn = curclass.substr(0, curclass.length() - 1);
						string old_inherits = myenv[curn + ".__inherits__"].str;
						if (old_inherits == "null" || old_inherits == "") old_inherits = "";
						else old_inherits += ",";
						myenv.set_global(curn + ".__inherits__", old_inherits + to_inherit);
						// Run inheritance
						myenv.copy_inherit(to_inherit, curn);
						inh_map.unions(to_inherit, curn);
					}
				}
				else if (codexec[0] == "shared") {
					string &to_share = codexec[1];
					while (to_share.length() && to_share[to_share.length() - 1] == '\n') to_share.pop_back();
					if (to_share == "class") {
						myenv.set_global(curclass + "__shared__", intValue("1"));
					}
					else {
						string to_set = curclass + to_share;
						if (!myenv.count(to_set)) {
							myenv.declare_global(to_set);
						}
						myenv.set_global(to_set + ".__shared__", intValue("1"));
					}
				}
				else if (codexec[0] == "must_inherit") {
					myenv.set_global(curclass + "__must_inherit__", intValue("1"));
				}
				else if (codexec[0] == "no_inherit") {
					myenv.set_global(curclass + "__no_inherit__", intValue("1"));
				}
				else if (codexec[0] == "hidden") {
					parameter_check(2);
					string &ce = codexec[1];
					while (ce.length() && (ce[0] == '\n')) ce.erase(ce.begin());
					while (ce.length() && (ce[ce.length() - 1] == '\n')) ce.pop_back();
					myenv.set_global(curclass + ce + ".__hidden__", intValue("1"));
				}
				break;
			default:
				break;
			}
			if (codexec[0] == "function") {
				parameter_check(2);
				fun_indent = 1 + bool(curclass.length());
				if (codexec.size() >= 3) {
					if (codexec[2][codexec[2].length() - 1] == '\n') codexec[2].pop_back();
					codexec[2].pop_back(); // ':'
					cfargs = codexec[2];
				}
				else {
					cfargs = "";
					if (codexec[1][codexec[1].length() - 1] == '\n') codexec[1].pop_back();
					codexec[1].pop_back(); // ':'
				}
				cfname = curclass + codexec[1];
			}
		}

	}
	if (cfname.length()) {
		myenv.set_global(cfname, curfun);
		myenv.set_global(cfname + ".__type__", intValue("function"));
		myenv.set_global(cfname + ".__arg__", cfargs);
	}

	//return null;
	// End

	intValue res = run(code, myenv, "Main function");

	// For debug propose
	//myenv.dump();
	return res;
}

char post_buf1[8192] = {};

int main(int argc, char* argv[]) {
	stdouth = GetStdHandle(STD_OUTPUT_HANDLE);

	// Test: Input code here:
#pragma region Compiler Test Option
#if _DEBUG
	// Warning: When testing VBWeb can't use it
	string code = "", file = "";
	target_path = "";
	in_debug = false;
	no_lib = false;

	if (code.length()) {
		specialout();
		cout << code;
		cout << endl << "-------" << endl;
		endout();
	}
#else
	// DO NOT CHANGE
	string code = "", file = "";
	in_debug = false;
	no_lib = false;
#endif
	string version_info = string("BluePage Interpreter\nVersion 2.0\nIncludes:\n\nBlueBetter Interpreter\nVersion 1.10a\nCompiled on ") + __DATE__ + " " + __TIME__;
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

	env_name = file;
	map<string, string> reqs = { {"FILE_NAME", intValue(file).unformat()}, {"__BLUEPAGE__", intValue(1).unformat()} };
	map<string, bcaller> callers;	// Insert your requirements here

	for (int i = 2; i < argc; i++) {
		string opt = argv[i];
		if (opt == "--debug") {
			in_debug = true;
		}
		else if (beginWith(opt, "--const:")) {
			// String values only
			vector<string> spl = split(opt, ':', 1);
			vector<string> key_value = split(spl[1], '=', 1);
			reqs[key_value[0]] = intValue(key_value[1]).unformat();
		}
		else if (beginWith(opt, "--target:")) {
			vector<string> spl = split(opt, ':', 1);
			target_path = spl[1];
		}
	}
#pragma endregion

	if (target_path.length() <= 0) {
		curlout();
		cout << "Error: Target path not given. use --target:Path." << endl;
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

	if (in_debug) {
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

	auto initial_echo = [](string exp, varmap &env) -> intValue {
		intValue output = calcute(exp, env);
		header += output.str;
		return null;
	};

	auto normal_echo = [](string exp, varmap &env) -> intValue {
		intValue output = calcute(exp, env);
		content += output.str;
		return null;
	};

	varmap keep_env;
	keep_env.push();
	size_t next_pos = 0, previous_pos = 0;
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
			preRun(current_code, keep_env, reqs, { {string("bluecho"), initial_echo} });
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
			string &myself = reqs["SELF_POST"];
			while (myself.length() && myself[0] == '"') myself.erase(myself.begin());
			while (myself.length() && myself[myself.length() - 1] == '"') myself.pop_back();
			// Should be provided:
			// Matches 'xhr.setRequestHeader('MinServerPostBack','1');' in the header.
			string &is_postback = reqs["IS_POSTBACK"];	// To deal with postback, 0 or 1
			string my_bef_send = "", my_aft_send = "";
			// Also deal with postback in the field
			if (is_postback == "\"1\"") {
				// To be written... serial object into postback support, also send commands back.
				// AND: ANYTHING AFTER IT will be ignored!!!
				preRun("postback._inside_process", keep_env, reqs, { {string("bluecho"), normal_echo} });
				end_of_postback = true;
				break;
			}
			if (myself == "") {
				raise_global_ce("Cannot use postback: SELF_POST is not supported by Server");
			}
			else if (postback_set) {
				raise_global_ce("Cannot add 2 or more postback description in a file");
			}
			else {
				postback_set = true;
				vector<string> exprs = split(current_code), curcmd, descmd;
				content += "<script>\n";

				// Add object-liked string for 'onpostback'.
				string onloadcall = "window.onload = function() {\n", onpostback = "function mins_postback(info,para) {\n	var sending = \"__object$\\n.__type__=object\\n\";\n";

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
					else if (curcmd[0] == "on_load") {
						onloadcall += "	mins_postback('" + curcmd[1] + "');\n";	
					}
					else {
						raise_global_ce("Bad postback description");
					}
				}

				onloadcall += "\n};";
				onpostback += "\n	if (info != null) sending += '.field=\"' + mins_format(info.toString()) + '\"';\n	if (para != null) sending += '\\n.parameter=\"' + mins_format(para.toString()) + '\"';\n	var xhr = new XMLHttpRequest();\n	new Promise(function(r,rj){";
				if (my_bef_send.length()) onpostback += my_bef_send + "();";
				onpostback += "r(null);}).then(function(arg){xhr.open('POST', '/" + myself +"', false); if (info != null) {xhr.setRequestHeader('MinServerPostBack','1');} xhr.send(sending); mins_dealing(xhr.responseText); })";
				if (my_aft_send.length()) onpostback += ".then(function(arg){" + my_aft_send + "();})";
				onpostback += ";}";
				// Read Postback processor.
				FILE *fread = fopen("Postback.js", "r");
				if (fread == NULL) {
					raise_global_ce("Cannot add JavaScript support of PostBack");
				}
				else {
					while (!feof(fread)) {
						fgets(post_buf1, 8192, fread);
						content += string(post_buf1) + "\n";
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
			preRun(current_code, keep_env, reqs, { {string("bluecho"), normal_echo} });
		}
		previous_pos = end_pos + blue_end.length();
		next_pos = end_pos;
	}
	while (content.length() && (content[0] == '\n' || content[0] == '\r')) content.erase(content.begin());
	while (header.length() && (header[header.length() - 1] == '\n' || header[header.length() - 1] == '\r')) header.pop_back();
	if (!end_of_postback) content += code.substr(previous_pos);
	FILE *fout = fopen(target_path.c_str(), "w");
	fprintf(fout, "%s\n", header.c_str());
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
	fclose(fout);

	return 0;
}
