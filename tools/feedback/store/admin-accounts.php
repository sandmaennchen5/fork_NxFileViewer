<?php
declare(strict_types=1);
require_once __DIR__.'/admin-security.php';
require_once __DIR__.'/admin-totp.php';
function adminPermissions(): array { return ['view_polls'=>'Umfragen ansehen','manage_polls'=>'Umfragen erstellen / schließen','delete_polls'=>'Umfragen löschen','view_support'=>'Support und Kennungen ansehen','download_logs'=>'Private Logs herunterladen','delete_logs'=>'Private Logs löschen','view_statistics'=>'Statistik ansehen','manage_reports'=>'Meldungen übertragen / erneut versuchen','manage_users'=>'Benutzer verwalten']; }
function adminPreset(string $name): array { $show=['view_polls','view_support','view_statistics']; return match($name){'admin'=>array_keys(adminPermissions()),'polls'=>[...$show,'manage_polls'],default=>$show}; }
function adminNormalizePermissions(array $values): array {
    foreach($values as $value)if(!is_string($value)||!isset(adminPermissions()[$value]))throw new DomainException('Ungültige Berechtigung.');
    if(array_intersect($values,['manage_polls','delete_polls']))$values[]='view_polls';
    if(array_intersect($values,['download_logs','delete_logs']))$values[]='view_support';
    return array_values(array_unique($values));
}
function adminCan(string $permission): bool { global $adminUser; return isset($adminUser)&&in_array($permission,json_decode($adminUser['permissions'],true,flags:JSON_THROW_ON_ERROR),true); }
function adminRequirePermission(string $permission): void { if(!adminCan($permission)){http_response_code(403);exit(function_exists('webT')?webT('Keine Berechtigung.'):'Keine Berechtigung.');} }
function adminUserBy(PDO $db,string $column,string $value): ?array { if(!in_array($column,['id','username'],true))throw new LogicException();$q=$db->prepare("SELECT * FROM admin_users WHERE $column=?");$q->execute([$value]);return $q->fetch(PDO::FETCH_ASSOC)?:null; }
function adminUsersSchema(PDO $db,array $config): void {
    databaseSqliteSchema($db,'CREATE TABLE IF NOT EXISTS admin_users (id TEXT PRIMARY KEY, username TEXT NOT NULL UNIQUE, password_hash TEXT NOT NULL, permissions TEXT NOT NULL, totp_required INTEGER NOT NULL DEFAULT 0, totp_secret TEXT NULL, totp_last_step INTEGER NOT NULL DEFAULT -1, recovery_hashes TEXT NOT NULL, auth_version INTEGER NOT NULL DEFAULT 1, created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL, last_login_at INTEGER NULL)');
    if((int)$db->query('SELECT COUNT(*) FROM admin_users')->fetchColumn()!==0)return;
    $hash=$config['admin_password_hash']??$config['poll_admin_password_hash']??'';
    if(!is_string($hash)||password_get_info($hash)['algo']===null)throw new RuntimeException('Missing bootstrap password');
    $sql=databaseSql($db,'INSERT OR IGNORE INTO admin_users','INSERT IGNORE INTO admin_users');
    $q=$db->prepare($sql.' (id,username,password_hash,permissions,recovery_hashes,created_at,updated_at) VALUES (?,?,?,?,?,?,?)');
    $q->execute([substr(hash('sha256','NxFileViewer bootstrap admin'),0,32),'admin',$hash,json_encode(adminPreset('admin')),'[]',time(),time()]);
}
function adminFinishLogin(PDO $db,array $user,bool $mfa=false): void {
    adminResetSession();$_SESSION['poll_admin']=true;$_SESSION['admin_user_id']=$user['id'];$_SESSION['admin_user_version']=(int)$user['auth_version'];
    $_SESSION['admin_started_at']=$_SESSION['admin_last_seen']=time();if($mfa)$_SESSION['admin_mfa_at']=time();
    $db->prepare('UPDATE admin_users SET last_login_at=? WHERE id=?')->execute([time(),$user['id']]);
}
function adminReauthenticate(PDO $db,array $config,array $policy,array $user): void {
    adminReserveLogin($db,$config,$policy,(string)($_SERVER['REMOTE_ADDR']??''));
    $password=$_POST['confirm_password']??'';
    if(!is_string($password)||strlen($password)>1024||!password_verify($password,$user['password_hash']))throw new DomainException('Eigenes Passwort ist nicht korrekt.');
    if($user['totp_secret']!==null && time()-(int)($_SESSION['admin_mfa_at']??0)>=300){
        if(!adminConsumeSecondFactor($db,$config,$user,(string)($_POST['confirm_code']??'')))throw new DomainException('Aktueller 2FA-Code erforderlich.');
        $_SESSION['admin_mfa_at']=time();
    }
}
function adminPasswordHash(string $password): string { if(strlen($password)<12||strlen($password)>72)throw new DomainException('Passwort: 12 bis 72 Bytes erforderlich.');return password_hash($password,PASSWORD_DEFAULT); }
function adminSaveUser(PDO $db,array $actor,array $input): void {
    $id=(string)($input['id']??'');$action=(string)($input['action']??'');
    $db->beginTransaction();
    try{
        $users=$db->query('SELECT * FROM admin_users ORDER BY id'.(databaseIsMariaDb($db)?' FOR UPDATE':''))->fetchAll(PDO::FETCH_ASSOC);
        $current=null;$target=null;$managers=0;
        foreach($users as $u){if($u['id']===$actor['id'])$current=$u;if($u['id']===$id)$target=$u;if(in_array('manage_users',json_decode($u['permissions'],true),true))$managers++;}
        if(!$current||(int)$current['auth_version']!==(int)$actor['auth_version']||!in_array('manage_users',json_decode($current['permissions'],true),true))throw new DomainException('Sitzung ungültig.');
        if(($id!==''||in_array($action,['delete-user','reset-2fa'],true))&&!$target)throw new DomainException('Benutzer nicht gefunden.');
        if($action==='delete-user'){
            if($id===$actor['id'])throw new DomainException('Das eigene Konto kann nicht gelöscht werden.');
            if(($input['confirm_delete']??'')!=='yes')throw new DomainException('Löschen bitte bestätigen.');
            if(in_array('manage_users',json_decode($target['permissions'],true),true)&&$managers<=1)throw new DomainException('Der letzte Administrator muss erhalten bleiben.');
            $db->prepare('DELETE FROM admin_users WHERE id=?')->execute([$id]);
        }elseif($action==='reset-2fa'){
            $db->prepare("UPDATE admin_users SET totp_secret=NULL,totp_last_step=-1,recovery_hashes='[]',auth_version=auth_version+1,updated_at=? WHERE id=?")->execute([time(),$id]);
        }else{
            $name=strtolower(trim((string)($input['username']??'')));if(!preg_match('/\A[a-z0-9_.-]{3,64}\z/',$name))throw new DomainException('Benutzername: 3–64 Zeichen, a–z, 0–9, Punkt, _ oder -.');
            foreach($users as $existing)if($existing['username']===$name&&$existing['id']!==$id)throw new DomainException('Benutzername ist bereits vergeben.');
            $permissions=adminNormalizePermissions($input['permissions']??[]);
            if($target&&in_array('manage_users',json_decode($target['permissions'],true),true)&&!in_array('manage_users',$permissions,true)&&$managers<=1)throw new DomainException('Der letzte Administrator muss erhalten bleiben.');
            $password=(string)($input['new_password']??'');$hash=$password!==''?adminPasswordHash($password):($target['password_hash']??adminPasswordHash(''));
            $required=isset($input['totp_required'])?1:0;
            if($target)$db->prepare('UPDATE admin_users SET username=?,password_hash=?,permissions=?,totp_required=?,auth_version=auth_version+1,updated_at=? WHERE id=?')->execute([$name,$hash,json_encode($permissions),$required,time(),$id]);
            else $db->prepare("INSERT INTO admin_users (id,username,password_hash,permissions,totp_required,recovery_hashes,created_at,updated_at) VALUES (?,?,?,?,?,'[]',?,?)")->execute([bin2hex(random_bytes(16)),$name,$hash,json_encode($permissions),$required,time(),time()]);
        }
        $db->commit();
    }catch(Throwable $e){if($db->inTransaction())$db->rollBack();throw $e;}
}

function adminUpdatedUser(PDO $db,array $previous): array {
    $updated=adminUserBy($db,'id',$previous['id']);
    if(!$updated||(int)$updated['auth_version']!==(int)$previous['auth_version']+1)
        throw new DomainException('Das Konto wurde zwischenzeitlich geändert. Bitte erneut anmelden.');
    return $updated;
}
