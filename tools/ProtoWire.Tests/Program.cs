using System;
using Google.Protobuf;

internal static class Program
{
    private static int Main()
    {
        try
        {
            var request = new RegisterReq
            {
                Account = "unity-account",
                Password = "pw",
                ServerId = 1,
            };
            var unityWire = request.ToByteArray();
            Assert(Convert.ToHexString(unityWire) == "0A0D756E6974792D6163636F756E74120270771801",
                $"unexpected Unity wire: {Convert.ToHexString(unityWire)}");

            Assert(RegisterReq.AccountFieldNumber == 1, "account field must remain 1");
            Assert(RegisterReq.PasswordFieldNumber == 2, "password field must remain 2");
            Assert(RegisterReq.ServerIdFieldNumber == 3, "server_id field must remain 3");
            Assert(RegisterReq.Descriptor.Fields.InDeclarationOrder().Count == 3,
                "Unity RegisterReq must not contain server-internal header fields");

            // Same business payload with Go-only uID/serverID/sessionID fields 101-103.
            // Google.Protobuf must preserve the business data while treating those fields as unknown.
            var goWire = Convert.FromHexString(
                "0A0D756E6974792D6163636F756E74120270771801" +
                "A80663B00607BA060773657373696F6E");
            var decoded = RegisterReq.Parser.ParseFrom(goWire);
            Assert(decoded.Account == "unity-account", "Go wire account mismatch");
            Assert(decoded.Password == "pw", "Go wire password mismatch");
            Assert(decoded.ServerId == 1, "Go wire server_id mismatch");

            Console.WriteLine("Proto wire compatibility tests passed (C# <-> Go).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
