<?php
if(!isset($adminUser)){http_response_code(403);exit;}
$codes=$_SESSION['recovery_codes']??[];unset($_SESSION['recovery_codes']);
?>
<h2><?=webT('Mein Konto · 2FA')?></h2>
<?php if($codes):?><article><h3><?=webT('Wiederherstellungscodes jetzt sicher aufbewahren')?></h3><p><?=webT('Jeder Code funktioniert einmal. Diese Liste wird nur einmal angezeigt.')?></p><pre><?=adminEscape(implode("\n",$codes))?></pre></article><?php endif ?>
<p><?=$adminUser['totp_secret']!==null?webT('2FA ist aktiv.'):webT('2FA ist nicht aktiv.')?> <?=$adminUser['totp_required']?webT('Für dieses Konto ist 2FA Pflicht.'):webT('Die Einrichtung ist optional.')?></p>
<?php if($adminUser['totp_secret']===null):?>
<?php if(isset($_SESSION['optional_secret'])&&time()-(int)$_SESSION['optional_at']<=300):?>
<p><?=webT('In der Authenticator-App ein zeitbasiertes Konto (6 Stellen, 30 Sekunden) anlegen: NxFileViewer Admin / ')?><?=adminEscape($adminUser['username'])?>.</p><p><?=webT('Schlüssel: ')?><code><?=adminEscape($_SESSION['optional_secret'])?></code></p>
<form method="post"><?=adminCsrf()?><label><?=webT('Authenticator-Code ')?><input name="code" autocomplete="one-time-code" required></label><button name="action" value="confirm-2fa"><?=webT('2FA aktivieren')?></button></form>
<?php else:?><form method="post"><?=adminCsrf()?><?php adminConfirmation();?><button name="action" value="start-2fa"><?=webT('2FA einrichten')?></button></form><?php endif ?>
<?php else:?>
<form method="post"><?=adminCsrf()?><?php adminConfirmation();?><button name="action" value="recovery-codes"><?=webT('Neue Wiederherstellungscodes erzeugen')?></button><?php if(!$adminUser['totp_required']):?><button name="action" value="disable-2fa"><?=webT('2FA deaktivieren')?></button><?php endif ?></form>
<?php endif ?>
