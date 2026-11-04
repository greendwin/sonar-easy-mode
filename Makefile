# SONAR Easy Mode – build & deploy helper.
#
#   make                 build Release and copy the DLL into the game (same as `make deploy`)
#   make build           build only
#   make deploy          build + copy to $(GAME_DIR)\BepInEx\plugins\SonarEasyMode
#   make package         build + zip ready for players (dist\SonarEasyMode-<version>.zip)
#   make clean           remove build output
#
# Machine-specific paths live in local.mk (git-ignored). Start from local.mk.example:
#   cp local.mk.example local.mk     then set GAME_DIR (and optionally DOTNET)
# Command-line overrides still win:  make GAME_DIR="E:/Games/SONAR"   make CONFIG=Debug
# Recipes only call dotnet and powershell, so they work whether make uses sh or cmd.

-include local.mk

CONFIG     ?= Release
VERSION    ?= 0.1.0

# Prefer the per-user .NET 8 SDK (see README); fall back to whatever `dotnet` is on PATH.
USER_DOTNET := $(subst \,/,$(USERPROFILE))/.dotnet/dotnet.exe
DOTNET     ?= $(if $(wildcard $(USER_DOTNET)),$(USER_DOTNET),dotnet)

PROJECT    := SonarEasyMode/SonarEasyMode.csproj
DLL        := SonarEasyMode/bin/$(CONFIG)/net6.0/SonarEasyMode.dll
PLUGIN_DIR := $(GAME_DIR)/BepInEx/plugins/SonarEasyMode
DIST_DIR   := dist
STAGE_DIR  := $(DIST_DIR)/stage
ZIP        := $(DIST_DIR)/SonarEasyMode-$(VERSION).zip

PS := powershell -NoProfile -ExecutionPolicy Bypass -Command

.PHONY: all build deploy package clean check-gamedir check-bepinex

all: deploy

check-gamedir:
ifeq ($(strip $(GAME_DIR)),)
	$(error GAME_DIR is not set. Copy local.mk.example to local.mk and set GAME_DIR, or run: make GAME_DIR="X:/path/to/SONAR")
endif

check-bepinex: check-gamedir
	@$(PS) "if (-not (Test-Path '$(GAME_DIR)/BepInEx/interop/Assembly-CSharp.dll')) { Write-Error 'BepInEx interop not found in $(GAME_DIR). Install BepInEx and launch the game once (see README), or pass GAME_DIR=...'; exit 1 }"

build: check-bepinex
	"$(DOTNET)" build "$(PROJECT)" -c $(CONFIG) -p:Deploy=false -p:GameDir="$(GAME_DIR)" -nologo -v:minimal

deploy: build
	@$(PS) "if (Get-Process SONAR -ErrorAction SilentlyContinue) { Write-Error 'SONAR is running - close the game first (the DLL is locked).'; exit 1 }"
	@$(PS) "New-Item -ItemType Directory -Force -Path '$(PLUGIN_DIR)' | Out-Null; Copy-Item -Force '$(DLL)' '$(PLUGIN_DIR)'; Write-Host 'Deployed to $(PLUGIN_DIR)'"

package: build
	@$(PS) "Remove-Item -Recurse -Force '$(STAGE_DIR)' -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force -Path '$(STAGE_DIR)/BepInEx/plugins/SonarEasyMode' | Out-Null; Copy-Item '$(DLL)' '$(STAGE_DIR)/BepInEx/plugins/SonarEasyMode'; Copy-Item 'README.md' '$(STAGE_DIR)/BepInEx/plugins/SonarEasyMode'; Compress-Archive -Force -Path '$(STAGE_DIR)/*' -DestinationPath '$(ZIP)'; Remove-Item -Recurse -Force '$(STAGE_DIR)'; Write-Host 'Created $(ZIP)'"

clean:
	@$(PS) "Remove-Item -Recurse -Force 'SonarEasyMode/bin','SonarEasyMode/obj','$(DIST_DIR)' -ErrorAction SilentlyContinue"
