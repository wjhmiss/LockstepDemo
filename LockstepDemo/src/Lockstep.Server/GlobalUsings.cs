// 服务端工程的手动“隐式 using”集合。与 Shared 的 GlobalUsings 重复的部分没关系，
// 编译器对重复的 global using 不报错(同一文件多次声明同名空间才是 enabled 才行, 这里跨工程)。
global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.IO;
global using System.Net;
global using System.Net.Sockets;
global using System.Text;
global using System.Threading;
