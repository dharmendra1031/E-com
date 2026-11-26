using EcommerceWebApplication.Helper;

const string password = "Example-Test-Passphrase";
void Check(bool success, string message) { if (!success) throw new Exception(message); }
var first = StaffPassword.Hash(password);
var second = StaffPassword.Hash(password);
Check(first != password && first != second, "Hash must be salted.");
Check(StaffPassword.Verify(first, password, false, out var upgrade) && !upgrade, "Valid password rejected.");
Check(!StaffPassword.Verify(first, "wrong", true, out _), "Incorrect password accepted.");
Check(!StaffPassword.Verify(first, first, true, out _), "Stored hash accepted as password.");
Check(!StaffPassword.Verify(password, password, false, out _), "Legacy password accepted when disabled.");
Check(StaffPassword.Verify(password, password, true, out upgrade) && upgrade, "Legacy upgrade not requested.");
Check(!StaffPassword.Verify(password, password.ToLowerInvariant(), true, out _), "Legacy case mismatch accepted.");
Check(!StaffPassword.Verify(StaffPassword.Prefix + "invalid", StaffPassword.Prefix + "invalid", true, out _), "Malformed hash accepted.");
Check(!StaffPassword.IsValidNew("short") && StaffPassword.IsValidNew(password), "Password length validation incorrect.");
Console.WriteLine("Staff password checks passed.");
