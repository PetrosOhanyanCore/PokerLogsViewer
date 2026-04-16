cmake_minimum_required(VERSION 3.20)

project(PokerLogsViewer
    VERSION 1.0.0
    DESCRIPTION "Poker Logs Viewer - WPF/MVVM (.NET 8)"
    LANGUAGES NONE)

if(NOT WIN32)
    message(FATAL_ERROR "PokerLogsViewer targets Windows only (WPF).")
endif()

# --- Locate dotnet CLI ---------------------------------------------------
find_program(DOTNET_EXE dotnet REQUIRED)
message(STATUS "Using dotnet: ${DOTNET_EXE}")

# Verify that a .NET 8 SDK is installed.
execute_process(
    COMMAND ${DOTNET_EXE} --list-sdks
    OUTPUT_VARIABLE DOTNET_SDKS
    OUTPUT_STRIP_TRAILING_WHITESPACE)
if(NOT DOTNET_SDKS MATCHES "(^|\n)8\\.")
    message(WARNING
        "No .NET 8 SDK detected. Installed SDKs:\n${DOTNET_SDKS}\n"
        "Install it from https://dotnet.microsoft.com/download/dotnet/8.0")
endif()

# --- Paths ---------------------------------------------------------------
set(SOLUTION_FILE "${CMAKE_SOURCE_DIR}/PokerLogsViewer.sln")
set(PROJECT_FILE  "${CMAKE_SOURCE_DIR}/src/PokerLogsViewer.csproj")
set(PUBLISH_DIR   "${CMAKE_BINARY_DIR}/publish")
set(BUILD_CONFIG  "$<IF:$<CONFIG:Debug>,Debug,Release>")
set(OUTPUT_DIR    "${CMAKE_BINARY_DIR}/bin/${BUILD_CONFIG}")

# --- Track sources so CMake re-triggers on edits -------------------------
file(GLOB_RECURSE APP_SOURCES CONFIGURE_DEPENDS
    "${CMAKE_SOURCE_DIR}/src/*.cs"
    "${CMAKE_SOURCE_DIR}/src/*.xaml"
    "${CMAKE_SOURCE_DIR}/src/*.csproj"
    "${CMAKE_SOURCE_DIR}/*.sln")

# --- Default build target ------------------------------------------------
add_custom_target(PokerLogsViewer ALL
    COMMAND ${DOTNET_EXE} build "${SOLUTION_FILE}"
            -c ${BUILD_CONFIG}
            -o "${OUTPUT_DIR}"
            --nologo
    SOURCES ${APP_SOURCES}
    COMMENT "Building PokerLogsViewer (.NET 8, ${BUILD_CONFIG})"
    VERBATIM)

# --- Optional: restore dependencies only ---------------------------------
add_custom_target(restore
    COMMAND ${DOTNET_EXE} restore "${SOLUTION_FILE}" --nologo
    COMMENT "Restoring NuGet dependencies"
    VERBATIM)

# --- Optional: self-contained single-file publish ------------------------
add_custom_target(publish
    COMMAND ${DOTNET_EXE} publish "${PROJECT_FILE}"
            -c Release
            -r win-x64
            --self-contained true
            -p:PublishSingleFile=true
            -p:IncludeNativeLibrariesForSelfExtract=true
            -o "${PUBLISH_DIR}"
            --nologo
    COMMENT "Publishing self-contained win-x64 build to ${PUBLISH_DIR}"
    VERBATIM)

# --- Optional: clean dotnet artifacts ------------------------------------
add_custom_target(dotnet-clean
    COMMAND ${DOTNET_EXE} clean "${SOLUTION_FILE}" --nologo
    COMMENT "Cleaning dotnet build outputs"
    VERBATIM)
