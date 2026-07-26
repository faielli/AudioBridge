cmake_minimum_required(VERSION 3.18.1)
project(audiobridge_native C CXX)

set(OPUS_BUILD_SHARED_LIBRARY OFF CACHE BOOL "" FORCE)
set(OPUS_BUILD_TESTING    OFF CACHE BOOL "" FORCE)
set(OPUS_BUILD_PROGRAMS   OFF CACHE BOOL "" FORCE)
set(OPUS_CUSTOM_MODES     OFF CACHE BOOL "" FORCE)
set(OPUS_FIXED_POINT       ON CACHE BOOL "" FORCE)
set(OPUS_DISABLE_INTRINSICS ON CACHE BOOL "" FORCE)
set(OPUS_FIXED_POINT_DEBUG OFF CACHE BOOL "" FORCE)
set(OPUS_DRED              OFF CACHE BOOL "" FORCE)
set(OPUS_OSCE              OFF CACHE BOOL "" FORCE)
set(OPUS_DEEP_PLC          OFF CACHE BOOL "" FORCE)
set(OPUS_HARDENING         OFF CACHE BOOL "" FORCE)
set(OPUS_FUZZING           OFF CACHE BOOL "" FORCE)
set(OPUS_CHECK_ASM         OFF CACHE BOOL "" FORCE)
set(OPUS_FLOAT_APPROX      OFF CACHE BOOL "" FORCE)
set(OPUS_ASSERTIONS        OFF CACHE BOOL "" FORCE)
set(OPUS_STATIC_RUNTIME    OFF CACHE BOOL "" FORCE)
set(OPUS_INSTALL_PKG_CONFIG_MODULE  OFF CACHE BOOL "" FORCE)
set(OPUS_INSTALL_CMAKE_CONFIG_MODULE OFF CACHE BOOL "" FORCE)

add_subdirectory(opus)

set(OBOE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/oboe)
add_subdirectory(${OBOE_DIR} oboe_build)

add_library(opus_jni SHARED
    opus_wrapper.c
    audio_player.cpp
)
target_include_directories(opus_jni PRIVATE opus/include)
target_link_libraries(opus_jni
    opus
    oboe
)
