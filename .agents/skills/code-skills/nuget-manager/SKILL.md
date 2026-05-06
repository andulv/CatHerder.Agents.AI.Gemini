---
name: nuget-manager
description: Safe NuGet package management rules for .NET projects. Enforces using dotnet CLI commands instead of manually editing .csproj files.
user-invocable: false
---

# Skill: nuget-manager

> Safely manage NuGet packages in .NET projects.

## Purpose

Prevent common mistakes when adding, removing, or updating NuGet packages. The `dotnet` CLI is the only safe way to modify package references.

---

## Core Rules

### 1. NEVER Directly Edit `.csproj` to Add or Remove Packages

- **NEVER** manually add `<PackageReference>` elements to `.csproj` files
- **NEVER** manually remove `<PackageReference>` elements from `.csproj` files
- **ALWAYS** use `dotnet add package` and `dotnet remove package` CLI commands

### 2. Direct File Editing is ONLY Permitted for Version Updates

When updating a package version that already exists:

1. **Verify** the new version exists: `dotnet package search <name> --exact-match --format json`
2. **Find** where the version is managed (`.csproj`, `Directory.Packages.props`, or `Directory.Build.props`)
3. **Update** the version string in the file
4. **Restore**: `dotnet restore`
5. **Verify**: `dotnet build`

---

## Safe Commands

### Add a Package

```bash
dotnet add <project.csproj> package <PackageName>
dotnet add <project.csproj> package <PackageName> --version <version>
```

### Remove a Package

```bash
dotnet remove <project.csproj> package <PackageName>
```

### List Installed Packages

```bash
dotnet list <project.csproj> package
dotnet list <project.csproj> package --outdated
```

### Search for a Package

```bash
dotnet package search <name> --exact-match --format json
```

### Restore Packages

```bash
dotnet restore
```

---

## Version Update Workflow

```
1. Verify version exists
   └─> dotnet package search Newtonsoft.Json --exact-match --format json

2. Check management location
   └─> Is version in .csproj? Directory.Packages.props? Directory.Build.props?

3. Update the version string in the correct file

4. Restore and verify
   └─> dotnet restore && dotnet build
```

---

## Common Mistakes to Avoid

| Mistake | Why It's Wrong | Correct Approach |
|---------|---------------|-----------------|
| Edit `.csproj` to add package | May miss transitive dependencies, won't update lock file | `dotnet add package` |
| Edit `.csproj` to remove package | May leave orphan transitive deps | `dotnet remove package` |
| Update version without verifying | Version may not exist on feed | `dotnet package search` first |
| Skip `dotnet restore` after changes | Build will fail with missing refs | Always restore after changes |

---

## Applicability

This skill applies when:
- The project uses .NET (C#, F#, VB.NET)
- Package references are in `.csproj`, `.fsproj`, or centrally managed
- The `dotnet` CLI is available

For non-.NET projects, use the appropriate package manager:
- **Node.js**: `npm install` / `npm uninstall`
- **Python**: `pip install` / `pip uninstall`
