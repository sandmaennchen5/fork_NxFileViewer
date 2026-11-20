<?php
declare(strict_types=1);
if(PHP_SAPI!=='cli'){http_response_code(403);exit;}
require_once __DIR__.'/admin-accounts.php';
if(count($argv)!==3||$argv[2]!=='--reset-2fa'){fwrite(STDERR,"Aufruf: php admin-recover.php BENUTZER --reset-2fa\n");exit(1);}
try{
 $config=require __DIR__.'/config.php';$db=databaseOpen($config);adminUsersSchema($db,$config);
 $user=adminUserBy($db,'username',strtolower($argv[1]));if(!$user)throw new RuntimeException();
 $db->prepare("UPDATE admin_users SET totp_secret=NULL,totp_last_step=-1,recovery_hashes='[]',auth_version=auth_version+1,updated_at=? WHERE id=?")->execute([time(),$user['id']]);
 echo "2FA zurückgesetzt. Passwort und 2FA-Pflicht bleiben erhalten; alle Sitzungen sind ungültig.\n";
}catch(Throwable $e){fwrite(STDERR,"Wiederherstellung fehlgeschlagen.\n");exit(1);}
