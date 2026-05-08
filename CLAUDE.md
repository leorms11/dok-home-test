# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is a .NET solution (`HomeTest.sln`) in its initial state — no projects have been added yet.

## Solution

- **File:** `HomeTest.sln` (Visual Studio Solution, Format Version 12.00)
- **Configurations:** Debug|Any CPU, Release|Any CPU

## Commands

Once projects are added, standard .NET CLI commands apply:

```bash
# Build
dotnet build HomeTest.sln

# Run all tests
dotnet test HomeTest.sln

# Run a single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run in Release mode
dotnet build HomeTest.sln -c Release
```