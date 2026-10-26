// Compile the dependency-free exporter with the installed SDK's compiler.
// Avoids NuGet restores and works in restricted hosts where MSBuild cannot inspect /proc.
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const dotnet =
  process.env.DOTNET_HOST_PATH ||
  (process.env.DOTNET_ROOT
    ? path.join(
        process.env.DOTNET_ROOT,
        process.platform === "win32" ? "dotnet.exe" : "dotnet",
      )
    : "dotnet");
const versions = execFileSync(dotnet, ["--list-sdks"], { encoding: "utf8" })
  .trim()
  .split(/\r?\n/)
  .map((line) => line.match(/^(8\.0\.\d+) \[(.+)\]$/))
  .filter(Boolean)
  .sort((a, b) => a[1].localeCompare(b[1], undefined, { numeric: true }));
if (!versions.length)
  throw Error("Install a .NET 8 SDK to regenerate artwork.");
const sdk = versions.at(-1),
  sdkRoot = path.dirname(sdk[2]);
const packRoot = path.join(sdkRoot, "packs", "Microsoft.NETCore.App.Ref");
const pack = fs
  .readdirSync(packRoot)
  .filter((v) => v.startsWith("8.0."))
  .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
  .at(-1);
const refRoot = path.join(packRoot, pack, "ref", "net8.0");
const temp = fs.mkdtempSync(path.join(os.tmpdir(), "mosslight-art-"));
const sources = [
  ...fs
    .readdirSync(path.join(root, "vendor/ppc/Core"), { recursive: true })
    .filter((f) => f.endsWith(".cs"))
    .map((f) => path.join(root, "vendor/ppc/Core", f)),
  ...fs
    .readdirSync(path.join(root, "tools/art"))
    .filter((f) => f.endsWith(".cs"))
    .map((f) => path.join(root, "tools/art", f)),
  path.join(root, "vendor/ppc/EnvironmentArt.cs"),
];
try {
  const dll = path.join(temp, "Art.dll");
  const args = [
    "-nologo",
    "-target:exe",
    "-nullable:enable",
    "-out:" + JSON.stringify(dll),
    ...fs
      .readdirSync(refRoot)
      .filter((f) => f.endsWith(".dll"))
      .map((f) => "-r:" + JSON.stringify(path.join(refRoot, f))),
    ...sources.map((f) => JSON.stringify(f)),
  ];
  fs.writeFileSync(path.join(temp, "sources.rsp"), args.join("\n"));
  execFileSync(
    dotnet,
    [
      path.join(sdk[2], sdk[1], "Roslyn/bincore/csc.dll"),
      "@" + path.join(temp, "sources.rsp"),
    ],
    { stdio: "inherit", cwd: root },
  );
  fs.writeFileSync(
    path.join(temp, "Art.runtimeconfig.json"),
    JSON.stringify({
      runtimeOptions: {
        tfm: "net8.0",
        framework: { name: "Microsoft.NETCore.App", version: "8.0.0" },
      },
    }),
  );
  execFileSync(dotnet, [dll, path.join(root, "public/art")], {
    stdio: "inherit",
    cwd: root,
  });
} finally {
  fs.rmSync(temp, { recursive: true, force: true });
}
