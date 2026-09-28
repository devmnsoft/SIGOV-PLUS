let path = @"C:\MNSOFT\SIGOV-PLUS\src\Sigov.Api\bin\Debug\net10.0\Sigov.Application.dll"
printfn "mtime(utc): %s" (System.IO.File.GetLastWriteTimeUtc(path).ToString("yyyy-MM-dd HH:mm:ss"))
let asm = System.Reflection.Assembly.LoadFile(path)
printfn "asm: %s" asm.FullName
let t = asm.GetType("Sigov.Application.ComprasEmpresariais.AprovacaoFilaResumo")
if isNull t then printfn "TYPE NULL" else
  (for c in t.GetConstructors() do
     printfn "CTOR(%d):" (c.GetParameters().Length)
     for p in c.GetParameters() do
       printfn "  %s %s" (p.ParameterType.ToString()) p.Name)
