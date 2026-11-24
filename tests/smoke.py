"""Run against a running local RevLink API; creates unique test records."""
import base64, datetime as dt, json, os, urllib.request, urllib.error, uuid
BASE = os.environ.get('REVLINK_API','http://localhost:5080').rstrip('/')
# Valid 1x1 PNG
PHOTO = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGO48p8BAAR+AdRUoZymAAAAAElFTkSuQmCC'
def req(method,path,body=None,token=None,expected=200):
    data=json.dumps(body).encode() if body is not None else None
    headers={'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    try:
        with urllib.request.urlopen(urllib.request.Request(BASE+path,data=data,headers=headers,method=method),timeout=20) as r: code,raw=r.status,r.read()
    except urllib.error.HTTPError as e: code,raw=e.code,e.read()
    assert code==expected,(method,path,code,expected,raw)
    return json.loads(raw) if raw and raw.startswith((b'{',b'[')) else raw

def register(tag):
    suffix=uuid.uuid4().hex[:10]
    return req('POST','/api/auth/register',{'email':f'{tag}.{suffix}@example.com','password':'Test12345','driverTag':tag})
assert req('GET','/health')['status']=='ok'
a,b,c=[register(x) for x in ['DriverA','DriverB','DriverC']]
ta,tb,tc=[x['token'] for x in (a,b,c)]
assert req('POST','/api/auth/login',{'email':a['user']['email'],'password':'Test12345'})['user']['id']==a['user']['id']
req('POST','/api/auth/login',{'email':a['user']['email'],'password':'wrong123'},expected=401)
assert req('GET','/api/users/me',token=ta)['id']==a['user']['id']
car=req('POST','/api/cars',{'nickname':'SilverBlade','brand':'Honda','model':'Civic','year':2013,'engine':'1.8','style':'Street','photo':PHOTO},ta)['id']
assert any(x['id']==car for x in req('GET','/api/cars',token=tb))
post=req('POST','/api/posts',{'caption':'Smoke test','carId':car,'photo':PHOTO},ta)['id']
for _ in range(2):req('PUT',f'/api/posts/{post}/like',token=tb,expected=204)
assert next(x for x in req('GET','/api/posts',token=tb) if x['id']==post)['likeCount']==1
assert req('GET',f'/api/images/posts/{post}').startswith(b'\x89PNG')
now=dt.datetime.now(dt.timezone.utc)
event={'title':'Smoke meet','description':'Encontro de teste','location':'Garagem de teste','startAt':(now+dt.timedelta(hours=1)).isoformat(),'endAt':(now+dt.timedelta(hours=2)).isoformat(),'maxParticipants':2}
eid=req('POST','/api/events',event,ta)['id']
for _ in range(2):req('POST',f'/api/events/{eid}/join',token=tb,expected=204)
req('POST',f'/api/events/{eid}/join',token=tc,expected=400)
assert req('GET',f'/api/events/{eid}',token=ta)['participantCount']==2
req('PUT',f'/api/events/{eid}',event,tb,expected=403)
req('DELETE',f'/api/events/{eid}/members/{b["user"]["id"]}',token=ta,expected=204)
req('POST',f'/api/events/{eid}/join',token=tc,expected=204)
req('DELETE',f'/api/cars/{car}',token=tb,expected=403)
req('DELETE',f'/api/cars/{car}',token=ta,expected=204)
assert next(x for x in req('GET','/api/posts',token=ta) if x['id']==post)['carId'] is None
req('PUT','/api/users/me',{'driverTag':'DriverA edited','photo':PHOTO},ta)
req('DELETE',f'/api/posts/{post}',token=ta,expected=204)
req('DELETE',f'/api/events/{eid}',token=ta,expected=204)
print('OK: login/session, cars, posts, likes, images, events, permissions and profile')
