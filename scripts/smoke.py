"""Real HTTP integration checks. Set VERIFY_DOCKER=true to require all four runner stages."""
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

root=Path(__file__).resolve().parents[1]
with socket.socket() as sock:
    sock.bind(('127.0.0.1',0)); port=sock.getsockname()[1]
base=f'http://127.0.0.1:{port}'
key=secrets.token_urlsafe(32)
verify=os.environ.get('VERIFY_DOCKER','false').lower()=='true'
def request(path,body=None,authenticated=True):
    headers={'Content-Type':'application/json'}
    if authenticated: headers['X-Api-Key']=key
    req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
    try:
        with urllib.request.urlopen(req,timeout=15) as response:
            raw=response.read(); return response.status,json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        return error.code,error.read().decode()

with tempfile.TemporaryDirectory(prefix='bug-agent-smoke-') as temporary:
    env=os.environ.copy()
    env.update({'Agent__ApiKey':key,'ASPNETCORE_ENVIRONMENT':'Production','DataDirectory':temporary+'/data','WorkspaceDirectory':temporary+'/workspaces','Services__0__RepositoryPath':str(root),'Runner__Enabled':str(verify).lower()})
    env.pop('Model__ApiKey',None); env.pop('Model__Name',None); env.pop('GitHub__Token',None)
    with open(temporary+'/server.log','w+') as log:
        process=subprocess.Popen(['dotnet','run','--no-build','--project',str(root/'src/BugResolution.Api'),'--','--urls',base],cwd=root,env=env,stdout=log,stderr=log)
        try:
            for _ in range(150):
                try:
                    if request('/health')[0]==200: break
                except OSError: pass
                if process.poll() is not None: raise AssertionError('API exited before readiness')
                time.sleep(.1)
            else: raise AssertionError('API readiness timeout')
            assert request('/api/configuration',authenticated=False)[0]==401, 'Unauthenticated access must fail'
            assert request('/api/configuration')[0]==200
            assert request('/api/investigations',{})[0]==400, 'Malformed report must be rejected'
            report={'serviceId':'checkout-demo','title':'Paid checkout stays Pending','description':'Checkout payment succeeds but stored status stays Pending.','expected':'Paid order','actual':'Pending order','steps':'POST checkout then GET order','logs':'password=secretvalue','demo':True}
            code,run=request('/api/investigations',report); assert code==202,(code,run)
            run_id=run['id']; assert 'secretvalue' not in run['report']['logs']
            for _ in range(500 if verify else 100):
                _,run=request('/api/investigations/'+run_id)
                if run['status'] not in ('Queued','Running'): break
                time.sleep(1 if verify else .1)
            assert run['status']=='AwaitingReview',run
            assert run['proposal'] and run['diff'] and run['evidence']
            if verify:
                assert run['verification']=='Passed',run
                assert [item['exitCode'] for item in run['executions']]==[0,1,0,0],run
            else:
                assert run['verification']=='NotConfigured' and not run['executions'], 'Never fabricate verification'
            code,_=request('/api/investigations/'+run_id+'/review',{'decision':'Approved','notes':'Reviewed in smoke test'})
            assert code==200
            assert request('/api/investigations/'+run_id+'/review',{'decision':'Rejected','notes':'duplicate'})[0]==409
            assert request('/api/investigations/'+run_id+'/pull-request',{})[0]==409, 'Demo cannot publish PRs'
            report['demo']=False
            code,live=request('/api/investigations',report); assert code==202
            for _ in range(100):
                _,live=request('/api/investigations/'+live['id'])
                if live['status']=='Failed': break
                time.sleep(.1)
            assert live['status']=='Failed' and 'Configure Model' in live['error'], 'Missing model must not silently fall back to demo'
            print('PASS: API auth, report validation, redaction, durable investigation, proposal, verification status, review transition, PR guard and missing-model behavior.')
            if verify: print('PASS: isolated Docker baseline / failure-before / success-after / existing-suite checks.')
        except Exception:
            log.flush();log.seek(0);print(log.read()[-8000:]);raise
        finally:
            process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired: process.kill();process.wait()
