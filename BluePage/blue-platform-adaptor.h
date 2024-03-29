#pragma once
#include "blue-lib.h"
#define _CRT_NONSTDC_NO_WARNINGS
#ifdef _WIN32
#include <direct.h>
#include <io.h>
#else
#include <sys/types.h>
#include <sys/uio.h>
#include <dirent.h>
#include <sys/stat.h>
#endif

// Declarations in this file requires different codes in different platforms

// Preventing redeclaration
struct intValue;
class varmap;

enum blue_errors {
	no_error,
	yield_dir_fail
};

thread_local int blue_lasterr = blue_errors::no_error;

typedef intValue(*blue_dcaller)(varmap*);

#ifndef _WIN32
#define DWORD int
#define FOREGROUND_BLUE 0x1
#define FOREGROUND_GREEN 0x2
#define FOREGROUND_RED 0x4
#define FOREGROUND_INTENSITY 0x8
#define BACKGROUDN_BLUE 0x10
#define BACKGROUND_GREEN 0x20
#define BACKGROUND_RED 0x40
#define BACKGROUND_INTENSITY 0x80
#endif

#ifdef _WIN32
DWORD precolor = FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_GREEN, nowcolor = FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_GREEN;
#else
DWORD precolor = FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_GREEN | FOREGROUND_INTENSITY, nowcolor = FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_GREEN | FOREGROUND_INTENSITY;
#endif

enum environ_type {
	windows,
	linux,
	unix,
	mac,
	other
};

#if defined(_WIN32)
#define mkdir _mkdir
#define rmdir _rmdir
#endif

#if defined(_WIN32)
using fileinfo = _finddata_t;
using finder_handle = intptr_t;
#else
using finder_handle = long;
#endif

#if defined(_WIN32)
#define environ_type environ_type::windows
#elif defined(__linux__)
#define environ_type environ_type::linux
#elif defined(__unix__)
#define environ_type environ_type::unix
#elif defined(__APPLE__)
#define environ_type environ_type::mac
#else
#define environ_type environ_type::other
#endif

#if defined(_WIN32)
#include <Windows.h>

#pragma region CRT Adaptor
#define popen _popen
#pragma endregion


HANDLE stdouth;
void setColor(DWORD color) {
	SetConsoleTextAttribute(stdouth, color);
	precolor = nowcolor;
	nowcolor = color;
}

void standardPreparation() {
	stdouth = GetStdHandle(STD_OUTPUT_HANDLE);
}

void clearScreen() {
	system("cls");
}

#else

void clearScreen() {
	system("clear");
}

//HANDLE stdouth;
void setColor(int color) {
	static map<int, int> color_mapping = { {0x4, 1}, {0x2, 2}, {0x6, 3}, {0x1, 4}, {0x5, 5}, {0x3, 6}, {0x7, 7} };
	// Deal with foreground part:
	int fore = color & 7;
	int fore_result = 30, back_result = 40;
	if (color & 0x8) fore_result = 90;
	if (color & 0x80) back_result = 100;
	fore_result += color_mapping[fore];
	int back = color & 112;
	back_result += color_mapping[back >> 4];

	//SetConsoleTextAttribute(stdouth, color);
	if (back_result == 40) {
		printf("\033[%dm", fore_result);
	}
	else {
		printf("\033[%dm\033[%dm", fore_result, back_result);
	}
	precolor = nowcolor;
	nowcolor = color;
}

void standardPreparation() {
	
}

#endif

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
