<?php
declare(strict_types=1);
require_once dirname(__DIR__,2).'/store/web-language.php';
header('Cache-Control: no-store');
header('Referrer-Policy: no-referrer');
header('X-Content-Type-Options: nosniff');
header("Content-Security-Policy: default-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; form-action 'self'");
ini_set('display_errors', '0');
// HTTPS is determined by the web server, never by untrusted forwarded headers.
if (strtolower((string)($_SERVER['HTTPS'] ?? '')) !== 'on' && ($_SERVER['HTTPS'] ?? '') !== '1') {
    http_response_code(426);
    header('Content-Type: text/plain; charset=utf-8');
    exit(webT('HTTPS ist für die Verwaltung erforderlich.'));
}
header('Strict-Transport-Security: max-age=31536000');
require_once dirname(__DIR__,2).'/store/admin-accounts.php';
function adminEscape(string $value): string {return htmlspecialchars($value,ENT_QUOTES,'UTF-8');}
function adminNavigation(string $section): void {
    webLanguageSelector();
    echo '<nav>';
    foreach([''=>webT('Verwaltung'),'umfragen'=>webT('Umfragen'),'support'=>'Support','meldungen'=>webT('Meldungsarchiv'),'statistik'=>webT('Statistik'),'users'=>webT('Benutzer'),'account'=>webT('Mein Konto')] as $path=>$label){
        $permission=['umfragen'=>'view_polls','support'=>'view_support','meldungen'=>'view_support','statistik'=>'view_statistics','users'=>'manage_users'][$path]??null;
        if($permission&&!adminCan($permission))continue;
        echo '<a style="margin-right:16px" href="/admin/'.$path.'">'.$label.'</a>';
    }
    echo '<form method="post" action="/admin/"><input type="hidden" name="csrf" value="'.adminEscape($_SESSION['csrf']).webT('"><button name="action" value="logout">Abmelden</button></form></nav>');
}
$error='';$adminUser=null;$pending=null;$codes=[];
$section=match(rtrim((string)parse_url($_SERVER['REQUEST_URI']??'/admin/',PHP_URL_PATH),'/')){
 '/admin','/admin/index.php'=>'','/admin/umfragen'=>'umfragen','/admin/support'=>'support','/admin/meldungen'=>'meldungen','/admin/support/log'=>'support-log','/admin/support/ids'=>'support-ids','/admin/statistik'=>'statistik','/admin/users'=>'users','/admin/account'=>'account',default=>null};
if($section===null){http_response_code(404);exit;}
try{
 $config=require dirname(__DIR__,2).'/store/config.php';$security=adminSecurityPolicy($config);$sessionExpired=adminStartSession($security);
 $db=databaseOpen($config);adminUsersSchema($db,$config);
 if(!empty($_SESSION['poll_admin'])){
  $adminUser=adminUserBy($db,'id',(string)($_SESSION['admin_user_id']??''));
  if(!$adminUser||(int)$adminUser['auth_version']!==($_SESSION['admin_user_version']??0)||($adminUser['totp_required']&&!$adminUser['totp_secret'])){adminResetSession();$adminUser=null;}
 }
 if(isset($_SESSION['pending_user'])){
  $pending=adminUserBy($db,'id',$_SESSION['pending_user']);
  if(!$pending||(int)$pending['auth_version']!==($_SESSION['pending_version']??0)||time()-(int)($_SESSION['pending_at']??0)>300){adminResetSession();$pending=null;}
 }
 if(($_SERVER['REQUEST_METHOD']??'')==='POST'){
  if(!is_string($_POST['csrf']??null)||!hash_equals($_SESSION['csrf'],$_POST['csrf']))throw new DomainException(webT('Bitte die Seite neu laden.'));
  $action=(string)($_POST['action']??'');
  if($action==='logout'){adminResetSession();header('Location: /admin/',true,303);exit;}
  if($action==='login'){
   adminReserveLogin($db,$config,$security,(string)($_SERVER['REMOTE_ADDR']??''));
   $user=adminUserBy($db,'username',strtolower(trim((string)($_POST['username']??'admin'))));$password=$_POST['password']??'';
   $hash=$user['password_hash']??'$2y$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2uheWG/igi';
   if(!is_string($password)||strlen($password)>1024||!password_verify($password,$hash)||!$user)throw new DomainException(webT('Anmeldung fehlgeschlagen.'));
   if($user['totp_secret']!==null||$user['totp_required']){
    adminResetSession();$_SESSION['pending_user']=$user['id'];$_SESSION['pending_version']=(int)$user['auth_version'];$_SESSION['pending_at']=time();$_SESSION['pending_attempts']=0;
    if($user['totp_secret']===null)$_SESSION['enroll_secret']=adminTotpSecret();
   }else adminFinishLogin($db,$user);
   header('Location: /admin/',true,303);exit;
  }
  if($action==='second-factor'&&$pending){
   adminReserveLogin($db,$config,$security,(string)($_SERVER['REMOTE_ADDR']??''));
   if(++$_SESSION['pending_attempts']>5){adminResetSession();$pending=null;throw new DomainException(webT('Bitte erneut anmelden.'));}
   $code=(string)($_POST['code']??'');
   if($pending['totp_secret']===null){
    $step=adminTotpMatch($_SESSION['enroll_secret'],$code);if($step===null)throw new DomainException(webT('Code ungültig.'));
    $recovery=adminNewRecoveryCodes($config,$pending['id']);
    $q=$db->prepare('UPDATE admin_users SET totp_secret=?,totp_last_step=?,recovery_hashes=?,auth_version=auth_version+1 WHERE id=? AND auth_version=? AND totp_secret IS NULL');
    $q->execute([adminEncryptTotp($config,$_SESSION['enroll_secret']),$step,$recovery['hashes'],$pending['id'],$pending['auth_version']]);if($q->rowCount()!==1)throw new DomainException(webT('Bitte erneut anmelden.'));
    $pending=adminUpdatedUser($db,$pending);adminFinishLogin($db,$pending,true);$_SESSION['recovery_codes']=$recovery['plain'];
   }else{
    if(!adminConsumeSecondFactor($db,$config,$pending,$code))throw new DomainException(webT('Code ungültig oder bereits verwendet.'));
    adminFinishLogin($db,$pending,true);
   }
   header('Location: /admin/account',true,303);exit;
  }
  if($adminUser&&in_array($action,['save-user','delete-user','reset-2fa'],true)){
   adminRequirePermission('manage_users');adminReauthenticate($db,$config,$security,$adminUser);adminSaveUser($db,$adminUser,$_POST);
   if(($_POST['id']??'')===$adminUser['id']){adminResetSession();header('Location: /admin/',true,303);}else header('Location: /admin/users',true,303);exit;
  }
  if($adminUser&&in_array($action,['start-2fa','confirm-2fa','disable-2fa','recovery-codes'],true)){
   if($action==='confirm-2fa'){
    if(!isset($_SESSION['optional_secret'])||time()-(int)($_SESSION['optional_at']??0)>300)throw new DomainException(webT('Einrichtung abgelaufen.'));
    $step=adminTotpMatch($_SESSION['optional_secret'],(string)($_POST['code']??''));if($step===null)throw new DomainException(webT('Code ungültig.'));
    $recovery=adminNewRecoveryCodes($config,$adminUser['id']);
    $q=$db->prepare('UPDATE admin_users SET totp_secret=?,totp_last_step=?,recovery_hashes=?,auth_version=auth_version+1 WHERE id=? AND auth_version=? AND totp_secret IS NULL');
    $q->execute([adminEncryptTotp($config,$_SESSION['optional_secret']),$step,$recovery['hashes'],$adminUser['id'],$adminUser['auth_version']]);if($q->rowCount()!==1)throw new DomainException(webT('Bitte erneut anmelden.'));
    $adminUser=adminUpdatedUser($db,$adminUser);adminFinishLogin($db,$adminUser,true);$_SESSION['recovery_codes']=$recovery['plain'];
   }else{
    adminReauthenticate($db,$config,$security,$adminUser);
    if($action==='start-2fa'){
     if($adminUser['totp_secret']!==null)throw new DomainException(webT('2FA ist bereits aktiv.'));
     $_SESSION['optional_secret']=adminTotpSecret();$_SESSION['optional_at']=time();
    }elseif($action==='disable-2fa'){
     if($adminUser['totp_required'])throw new DomainException(webT('2FA ist für dieses Konto verpflichtend.'));
     $q=$db->prepare("UPDATE admin_users SET totp_secret=NULL,totp_last_step=-1,recovery_hashes='[]',auth_version=auth_version+1 WHERE id=? AND auth_version=?");$q->execute([$adminUser['id'],$adminUser['auth_version']]);if($q->rowCount()!==1)throw new DomainException(webT('Bitte erneut anmelden.'));
     $adminUser=adminUpdatedUser($db,$adminUser);adminFinishLogin($db,$adminUser);
    }else{
     if($adminUser['totp_secret']===null)throw new DomainException(webT('Zuerst 2FA aktivieren.'));
     $recovery=adminNewRecoveryCodes($config,$adminUser['id']);$q=$db->prepare('UPDATE admin_users SET recovery_hashes=?,auth_version=auth_version+1 WHERE id=? AND auth_version=?');$q->execute([$recovery['hashes'],$adminUser['id'],$adminUser['auth_version']]);if($q->rowCount()!==1)throw new DomainException(webT('Bitte erneut anmelden.'));
     $adminUser=adminUpdatedUser($db,$adminUser);adminFinishLogin($db,$adminUser,true);$_SESSION['recovery_codes']=$recovery['plain'];
    }
   }
   header('Location: /admin/account',true,303);exit;
  }
 }
}catch(Throwable $e){http_response_code($e instanceof AdminLoginRateLimited?429:($e instanceof DomainException?400:503));if($e instanceof AdminLoginRateLimited)header('Retry-After: '.$e->retryAfter);$error=$e instanceof DomainException?$e->getMessage():webT('Die Verwaltung ist derzeit nicht verfügbar.');}
if($error===''&&$adminUser){
 $routes=['meldungen'=>['view_support','admin-reports.php'],'umfragen'=>['view_polls','polls.php'],'support'=>['view_support','support.php'],'support-ids'=>['view_support','support-ids.php'],'support-log'=>['download_logs','private-log.php'],'statistik'=>['view_statistics','statistics.php']];
 if(isset($routes[$section])){adminRequirePermission($routes[$section][0]);define('NX_ADMIN_SECTION',$section);if($section==='umfragen')unset($_GET['api'],$_GET['results'],$_GET['image'],$_GET['poll']);require dirname(__DIR__).'/'.$routes[$section][1];exit;}
 if($section==='users')adminRequirePermission('manage_users');
}
function adminCsrf(): string{return '<input type="hidden" name="csrf" value="'.adminEscape($_SESSION['csrf']??'').'">';}
function adminConfirmation(): void {echo webT('<label>Eigenes Passwort <input type="password" name="confirm_password" autocomplete="current-password" required></label><label>2FA-Code (wenn letzte Bestätigung länger als 5 Minuten zurückliegt) <input name="confirm_code" autocomplete="one-time-code"></label>');}
?>
<!doctype html><html lang="<?=webLanguage()?>"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title><?=webT('NxFileViewer Verwaltung')?></title>
<style>:root{color-scheme:light dark;font-family:system-ui,sans-serif}body{max-width:950px;margin:40px auto;padding:0 20px;line-height:1.6}input,button,select{padding:8px;font:inherit}label{display:block;margin:12px 0}article,fieldset{border:1px solid #8886;border-radius:12px;padding:20px;margin:20px 0}table{width:100%;border-collapse:collapse}td,th{padding:8px;text-align:left}code{overflow-wrap:anywhere}a{color:light-dark(#0645ad,#8ab4ff)}</style><h1><?=webT('NxFileViewer Verwaltung')?></h1>
<?php if(!$adminUser)webLanguageSelector(); ?>
<?php if(!empty($sessionExpired)):?><p><?=webT('Deine Admin-Sitzung ist abgelaufen. Bitte erneut anmelden.')?></p><?php endif ?>
<?php if($error!==''):?><p role="alert"><?=adminEscape(webT($error))?></p><?php endif ?>
<?php if($pending): ?>
<h2><?= $pending['totp_secret']===null?webT('2FA muss eingerichtet werden'):webT('Zweiter Faktor') ?></h2>
<?php if($pending['totp_secret']===null):?><p><?=webT('In deiner Authenticator-App ein zeitbasiertes Konto mit 6 Stellen und 30 Sekunden anlegen. Schlüssel:')?></p><code><?=adminEscape($_SESSION['enroll_secret'])?></code><p><?=webT('Konto: ')?><?=adminEscape($pending['username'])?> · NxFileViewer Admin</p><?php endif ?>
<form method="post"><?=adminCsrf()?><label><?=webT('Authenticator-Code')?><?= $pending['totp_secret']!==null?webT(' oder Wiederherstellungscode'):'' ?> <input name="code" autocomplete="one-time-code" required></label><button name="action" value="second-factor"><?=webT('Bestätigen')?></button></form>
<?php elseif(!$adminUser):?>
<form method="post"><?=adminCsrf()?><label><?=webT('Benutzername ')?><input name="username" autocomplete="username" required></label><label><?=webT('Passwort ')?><input type="password" name="password" autocomplete="current-password" required></label><button name="action" value="login"><?=webT('Anmelden')?></button></form>
<?php else: adminNavigation($section); ?>
<p><?=webT('Angemeldet als ')?><strong><?=adminEscape($adminUser['username'])?></strong></p>
<?php if($section==='users'&&adminCan('manage_users')): require dirname(__DIR__).'/admin-users.php';
elseif($section==='account'): require dirname(__DIR__).'/admin-account.php';
else: ?><p><?=webT('Wähle oben einen Verwaltungsbereich aus.')?></p><?php endif ?>
<?php endif ?></html>
