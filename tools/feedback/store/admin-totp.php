<?php
declare(strict_types=1);

function adminTotpSecret(): string { return adminBase32Encode(random_bytes(20)); }
function adminBase32Encode(string $bytes): string {
    $alphabet='ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';$bits=0;$buffer=0;$result='';
    foreach (unpack('C*',$bytes) as $value) {
        $buffer=($buffer<<8)|$value;$bits+=8;
        while($bits>=5){$bits-=5;$result.=$alphabet[($buffer>>$bits)&31];}
        $buffer&=(1<<$bits)-1;
    }
    if($bits>0)$result.=$alphabet[($buffer<<(5-$bits))&31];
    return $result;
}
function adminBase32Decode(string $text): string {
    $text=strtoupper(rtrim($text,'='));$alphabet='ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
    if($text==='' || !preg_match('/\A[A-Z2-7]+\z/',$text))throw new InvalidArgumentException('Invalid TOTP secret');
    $bits=0;$buffer=0;$result='';
    foreach(str_split($text) as $letter) {
        $buffer=($buffer<<5)|strpos($alphabet,$letter);$bits+=5;
        if($bits>=8){$bits-=8;$result.=chr(($buffer>>$bits)&255);}
        $buffer&=(1<<$bits)-1;
    }
    return $result;
}
function adminTotpAt(string $secret,int $step,int $digits=6): string {
    if($step<0 || !in_array($digits,[6,8],true))throw new InvalidArgumentException('Invalid TOTP time');
    $counter=pack('N2',intdiv($step,4294967296),$step%4294967296);
    $digest=hash_hmac('sha1',$counter,adminBase32Decode($secret),true);
    $offset=ord($digest[19])&15;
    $value=unpack('N',substr($digest,$offset,4))[1]&0x7fffffff;
    return str_pad((string)($value%(10**$digits)),$digits,'0',STR_PAD_LEFT);
}
function adminTotpMatch(string $secret,string $code,?int $now=null,int $lastStep=-1): ?int {
    $code=preg_replace('/\s+/','',$code);
    if(!preg_match('/\A[0-9]{6}\z/',$code))return null;
    $current=intdiv($now??time(),30);
    for($step=max(0,$current-1);$step<=$current+1;$step++)
        if($step>$lastStep && hash_equals(adminTotpAt($secret,$step),$code))return $step;
    return null;
}
function adminCryptoKey(array $config,string $purpose): string {
    $secret=$config['admin_totp_key']??$config['support_secret']??$config['poll_secret']??'';
    if(!is_string($secret)||strlen($secret)<32)throw new RuntimeException('Configure stable admin encryption key');
    return hash_hkdf('sha256',$secret,32,'NxFileViewer.Admin.'.$purpose.'.v1');
}
function adminEncryptTotp(array $config,string $secret): string {
    $iv=random_bytes(12);$tag='';
    $cipher=openssl_encrypt($secret,'aes-256-gcm',adminCryptoKey($config,'totp'),OPENSSL_RAW_DATA,$iv,$tag,'',16);
    if($cipher===false)throw new RuntimeException('Cannot protect TOTP secret');
    return base64_encode($iv.$tag.$cipher);
}
function adminDecryptTotp(array $config,string $encrypted): string {
    $bytes=base64_decode($encrypted,true);
    if($bytes===false || strlen($bytes)<29)throw new RuntimeException('Invalid protected TOTP secret');
    $secret=openssl_decrypt(substr($bytes,28),'aes-256-gcm',adminCryptoKey($config,'totp'),OPENSSL_RAW_DATA,
        substr($bytes,0,12),substr($bytes,12,16));
    if($secret===false)throw new RuntimeException('Cannot unlock TOTP secret');
    return $secret;
}
function adminTotpUri(string $username,string $secret): string {
    return 'otpauth://totp/'.rawurlencode('NxFileViewer Admin:'.$username).'?'.http_build_query(
        ['secret'=>$secret,'issuer'=>'NxFileViewer Admin','algorithm'=>'SHA1','digits'=>6,'period'=>30],'','&',PHP_QUERY_RFC3986);
}
function adminRecoveryDigest(array $config,string $id,string $code): ?string {
    $normalized=strtoupper(preg_replace('/[\s-]+/','',$code));
    if(!preg_match('/\A[A-F0-9]{16}\z/',$normalized))return null;
    return hash_hmac('sha256',$id.':'.$normalized,adminCryptoKey($config,'recovery'));
}
function adminNewRecoveryCodes(array $config,string $id): array {
    $plain=[];$hashes=[];
    for($i=0;$i<8;$i++){
        $code=implode('-',str_split(strtoupper(bin2hex(random_bytes(8))),4));
        $plain[]=$code;$hashes[]=adminRecoveryDigest($config,$id,$code);
    }
    return ['plain'=>$plain,'hashes'=>json_encode($hashes,JSON_THROW_ON_ERROR)];
}
function adminConsumeSecondFactor(PDO $db,array $config,array $user,string $code,?int $now=null): bool {
    if($user['totp_secret']===null)return false;
    $step=adminTotpMatch(adminDecryptTotp($config,$user['totp_secret']),$code,$now,(int)$user['totp_last_step']);
    if($step!==null){
        $statement=$db->prepare('UPDATE admin_users SET totp_last_step=? WHERE id=? AND auth_version=? AND totp_last_step < ? AND totp_secret=?');
        $statement->execute([$step,$user['id'],$user['auth_version'],$step,$user['totp_secret']]);
        return $statement->rowCount()===1;
    }
    $digest=adminRecoveryDigest($config,$user['id'],$code);
    if($digest===null)return false;
    $hashes=json_decode($user['recovery_hashes'],true,flags:JSON_THROW_ON_ERROR);
    foreach($hashes as $index=>$hash){
        if(!is_string($hash)||!hash_equals($hash,$digest))continue;
        unset($hashes[$index]);
        $statement=$db->prepare('UPDATE admin_users SET recovery_hashes=? WHERE id=? AND auth_version=? AND recovery_hashes=?');
        $statement->execute([json_encode(array_values($hashes),JSON_THROW_ON_ERROR),$user['id'],$user['auth_version'],$user['recovery_hashes']]);
        return $statement->rowCount()===1;
    }
    return false;
}
