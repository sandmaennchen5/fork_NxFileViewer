<?php
if(!isset($adminUser)||!adminCan('manage_users')){http_response_code(403);exit;}
$edit=isset($_GET['edit'])?adminUserBy($db,'id',(string)$_GET['edit']):null;
$selected=$edit?json_decode($edit['permissions'],true):adminPreset((string)($_GET['preset']??'show'));
?>
<h2><?=webT('Benutzer')?></h2><table><tr><th>Name</th><th>2FA</th><th><?=webT('Aktionen')?></th></tr>
<?php foreach($db->query('SELECT id,username,totp_required,totp_secret FROM admin_users ORDER BY username') as $row): ?>
<tr><td><?=adminEscape($row['username'])?></td><td><?=$row['totp_secret']!==null?webT('Aktiv'):webT('Nicht eingerichtet')?><?=$row['totp_required']?webT(' · Pflicht'):''?></td><td><a href="/admin/users?edit=<?=adminEscape($row['id'])?><?=webT('">Bearbeiten')?></a></td></tr>
<?php endforeach ?></table>
<h2><?=$edit?webT('Benutzer bearbeiten'):webT('Benutzer anlegen')?></h2>
<?php if(!$edit):?><p><?=webT('Vorlage: ')?><a href="?preset=show">SHOW</a> · <a href="?preset=polls">SHOW+UMFRAGE</a> · <a href="?preset=admin">ADMIN</a></p><?php endif ?>
<form method="post"><?=adminCsrf()?><input type="hidden" name="id" value="<?=adminEscape($edit['id']??'')?>">
<label><?=webT('Benutzername <input name="username" value="')?><?=adminEscape($edit['username']??'')?>" required pattern="[a-z0-9_.-]{3,64}"></label>
<label><?=$edit?webT('Neues Passwort (leer = unverändert)'):webT('Passwort')?> <input type="password" name="new_password" autocomplete="new-password" minlength="12" maxlength="72" <?=$edit?'':'required'?>></label>
<fieldset><legend><?=webT('Einzelne Berechtigungen')?></legend><?php foreach(adminPermissions() as $key=>$label):?><label><input type="checkbox" name="permissions[]" value="<?=$key?>" <?=in_array($key,$selected,true)?'checked':''?>><?=adminEscape(webT($label))?></label><?php endforeach ?></fieldset>
<label><input type="checkbox" name="totp_required" <?=!empty($edit['totp_required'])?'checked':''?><?=webT('>2FA verpflichtend (Einrichtung bei nächster Anmeldung)')?></label>
<?php adminConfirmation(); ?><button name="action" value="save-user"><?=webT('Speichern')?></button></form>
<?php if($edit):?>
<article><h3><?=webT('2FA zurücksetzen')?></h3><p><?=webT('Alle Sitzungen und Wiederherstellungscodes dieses Kontos werden ungültig. Eine bestehende 2FA-Pflicht bleibt erhalten.')?></p><form method="post"><?=adminCsrf()?><input type="hidden" name="id" value="<?=adminEscape($edit['id'])?>"><?php adminConfirmation();?><button name="action" value="reset-2fa"><?=webT('2FA zurücksetzen')?></button></form></article>
<?php if($edit['id']!==$adminUser['id']):?><article><form method="post"><?=adminCsrf()?><input type="hidden" name="id" value="<?=adminEscape($edit['id'])?>"><?php adminConfirmation();?><label><input type="checkbox" name="confirm_delete" value="yes" required><?=webT('Benutzer endgültig löschen')?></label><button name="action" value="delete-user"><?=webT('Benutzer löschen')?></button></form></article><?php endif ?>
<?php endif ?>
